#!/usr/bin/perl
# STR-2a / STR-2 : method length and complexity proxy.
# A method is a signature line (has a parenthesised parameter list and no ';' before the
# '(' ) whose body opens with '{'.  Length = non-blank lines strictly inside the braces,
# minus the closing brace line.  Complexity proxy = decision keywords/operators.
use strict; use warnings;
my ($MAXLEN,$MAXCX)=(50,10);
for my $f (@ARGV) {
    open(my $FH,'<',$f) or next; my @L=<$FH>; close $FH;
    for (my $i=0;$i<@L;$i++) {
        my $l = $L[$i];
        next unless $l =~ /\(/;
        next if $l =~ /^\s*(\/\/|\/\*|<|using|namespace|\}|\{)/;
        next if $l =~ /\b(if|for|foreach|while|switch|catch|return|new|lock|using|is|as|=)\b/ && $l !~ /\)\s*(=>|\{)?\s*$/;
        # must look like a declaration: modifiers or return type, name( ... )
        next unless $l =~ /^\s*(?:public|private|protected|internal|static|sealed|override|virtual|abstract|partial|async|unsafe|extern|\s)*[A-Za-z_][\w<>\[\],\.\?]*\s+[A-Za-z_]\w*\s*\(/
                 or $l =~ /^\s*(?:public|private|protected|internal|static|sealed|override|abstract)?\s*[A-Z]\w*\s*\(/;  # ctor
        next if $l =~ /;\s*$/ && $l !~ /\)\s*\{?\s*$/;   # statement, not header
        # find opening brace (this line or a following line)
        my $j=$i; my $opened=0;
        for (;$j<@L && $j<$i+4;$j++) { if ($L[$j]=~/\{/) { $opened=1; last } }
        next unless $opened;
        # brace-match from $j
        my $depth=0; my $k=$j; my $end=-1;
        for (;$k<@L;$k++) {
            my $line=$L[$k]; $line =~ s{ //[^"]*$}{};
            my $o = () = $line =~ /\{/g; my $c = () = $line =~ /\}/g;
            $depth += $o - $c;
            if ($depth <= 0) { $end=$k; last }
        }
        next if $end < 0;
        my $body_start = $j; my $body_end = $end;
        # count non-blank lines strictly inside (exclude the line that only opens and the line that only closes)
        my $count=0; my $cx=0;
        for my $n ($body_start..$body_end) {
            my $line=$L[$n];
            my $t=$line; $t =~ s/^\s+|\s+$//g;
            next if $t eq '' ;
            next if $n==$body_start && $t =~ /^[^\{]*\{\s*$/ && $body_end!=$body_start;
            next if $n==$body_end && $t =~ /^\}\s*$/;
            $count++;
            $cx += () = $t =~ /\b(if|for|foreach|while|case|catch|\?\.|\?:)\b|\?\s*[^:]+:/g;
            $cx += () = $t =~ /&&|\|\|/g;
            $cx += () = $t =~ /\bswitch\b/g;
        }
        my $name = ($L[$i] =~ /(\w+)\s*\(/) ? $1 : '?';
        printf "LONG %s:%d %s() body=%d\n", $f, $i+1, $name, $count if $count > $MAXLEN;
        printf "COMPLEX %s:%d %s() cx~%d\n", $f, $i+1, $name, $cx if $cx > $MAXCX;
    }
}
