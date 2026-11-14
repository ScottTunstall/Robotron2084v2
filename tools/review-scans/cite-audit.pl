#!/usr/bin/perl
# CMT-11 citation audit v5 (span-accurate).
#   A) doc block cites Original source with no Disassembly citation
#   B) inside a Disassembly span: an address absent from asm/robomame.asm
#   C) inside a Disassembly span: a <c>LABEL</c> absent from asm/robomame.asm
#   E) inside a Disassembly span: nothing but a label and no address at all (CMT-11b:
#      an unlabelled routine must be cited by its memory address)
# Audio/** skipped for B/C/E: its source is the sound board (VSNDRM3.SRC), whose ROM and
# source are not in this repository, so those citations cannot be verified here.
use strict; use warnings;

open(my $D,'<','asm/robomame.asm') or die $!;
my $raw = do { local $/; <$D> }; my $up = uc $raw;
my %tok; while ($up =~ /\$([0-9A-F]{4})\b/g) { $tok{$1}=1 }
my %cover;
while ($raw =~ /^([0-9A-Fa-f]{4}):\s+((?:[0-9A-Fa-f]{2}\s+){1,6})/gm) {
    my ($s,$b)=($1,$2); my $n = () = $b =~ /[0-9A-Fa-f]{2}/g; my $a=hex($s);
    $cover{sprintf('%04X',$a+$_)}=1 for 0..$n-1;
}
my %label; while ($up =~ /\b([A-Z][A-Z0-9_]{2,})\b/g) { $label{$1}=1 }
sub known { my $a=shift; $tok{$a} || $cover{$a} }
my %skip = map { $_=>1 } qw(
  ASM SRC ITEM SUMMARY REMARKS ROM CPU RAM MAME PNG RGB SHA URL API NONE NOTE TRUE FALSE ID
  OK TODO GET SET THE AND FOR ITS OF TO IN A IS AS BY ON DO NOT X Y GAME ORIGINAL
  DISASSEMBLY SOURCE NOTES SEE MONO XNA);

my ($A,$B,$C,$E)=(0,0,0,0);
for my $f (@ARGV) {
    my $isAudio = $f =~ m{/Audio/};
    open(my $FH,'<',$f) or next; my @L=<$FH>; close $FH;
    my ($in,$st,$doc)=(0,0,''); my @blocks;
    for my $i (0..$#L) {
        if ($L[$i] =~ /^\s*\/\/\//) { if(!$in){$in=1;$st=$i+1;$doc=''} $doc.=$L[$i] }
        elsif ($in) { push @blocks,[$st,$doc]; $in=0 }
    }
    push @blocks,[$st,$doc] if $in;
    for my $blk (@blocks) {
        my ($st,$doc)=@$blk; $doc =~ s/<item>/\x01/g;
        my $hasOrig = $doc =~ /Original source:/i; my $hasDis = $doc =~ /Disassembly:/i;
        if ($hasOrig && !$hasDis && !$isAudio) { print "A:NO-DISASSEMBLY $f:$st\n"; $A++ }
        for my $seg (split /\x01/, $doc) {
            next if $isAudio;
            my @marks;
            push @marks, ['O', $-[0]] while $seg =~ /Original source:/gi;
            push @marks, ['D', $-[0]] while $seg =~ /Disassembly:/gi;
            next unless @marks;
            @marks = sort { $a->[1] <=> $b->[1] } @marks;
            for my $m (0..$#marks) {
                my ($kind,$pos) = @{$marks[$m]};
                my $end = $m < $#marks ? $marks[$m+1][1] : length $seg;
                my $span = substr($seg, $pos, $end-$pos);
                next unless $kind eq 'D';
                my %s; for my $a (map {uc} $span =~ /\$([0-9A-Fa-f]{4})\b/g) {
                    next if $s{$a}++ || $a =~ /^0+$/;
                    unless (known($a)) { print "B:BAD-ADDR $f:$st \$$a\n"; $B++ }
                }
                my %ls; for my $lab ($span =~ /<c>([A-Z][A-Z0-9_]{2,})<\/c>/g) {
                    next if $ls{$lab}++ || $skip{$lab} || $lab =~ /\.ASM$|\.SRC$|\.PNG$/;
                    unless ($label{$lab}) { print "C:LABEL-ABSENT $f:$st $lab
"; $C++ }
                }
                unless ($span =~ /\$[0-9A-Fa-f]{4}/) { print "E:DISASSEMBLY-NO-ADDRESS $f:$st\n"; $E++ }
            }
        }
    }
}
print "== A=$A B=$B C=$C E=$E (Audio skipped for B/C/E)\n";
