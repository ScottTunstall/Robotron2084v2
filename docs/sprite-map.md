# Sprite map — extracted from robotron64k.bin (Release 5 solid blue)

24-bit colour PNGs at 1:1 arcade pixel size (4 bits/pixel; high nibble = left pixel).
Palette per WmsGfxSpriteEditor RobotronPaletteService (seanriddle.com ripper conversion).

- **Pass A**: offsets from the definitive sprite source (author directive) —
  `WmsGfxSpriteEditor.../Shared/Sprites/RobotronBlueLabelSpriteRepository.cs`
  (215 entries); `docs/robotronsprites.txt` is a committed mirror verified identical.
  Offsets are decimal into `ref/rom/robotron64k.bin`; width in bytes, height in px.
- **Pass B**: frames located in the original source listings, emitted only when the
  byte sequence is found verbatim in the ROM.

| name | source | ROM offset | size (px) | PNG | notes |
|------|--------|------------|-----------|-----|-------|
| familydeath | riddle list | $043B | 12×11 | Skull.png |  |
| 1000 | riddle list | $0499 | 12×5 | Score_1000.png |  |
| 2000 | riddle list | $04B7 | 12×5 | Score_2000.png |  |
| 3000 | riddle list | $04D5 | 12×5 | Score_3000.png |  |
| 4000 | riddle list | $04F3 | 12×5 | Score_4000.png |  |
| 5000 | riddle list | $0511 | 12×5 | Score_5000.png |  |
| mommy1 | riddle list | $055F | 8×14 | Mummy_1.png |  |
| mommy2 | riddle list | $0597 | 8×14 | Mummy_2.png |  |
| mommy3 | riddle list | $05CF | 8×14 | Mummy_3.png |  |
| mommy4 | riddle list | $0607 | 8×14 | Mummy_4.png |  |
| mommy5 | riddle list | $063F | 8×14 | Mummy_5.png |  |
| mommy6 | riddle list | $0677 | 8×14 | Mummy_6.png |  |
| mommy7 | riddle list | $06AF | 8×14 | Mummy_7.png |  |
| mommy8 | riddle list | $06E7 | 8×14 | Mummy_8.png |  |
| mommy9 | riddle list | $071F | 8×14 | Mummy_9.png |  |
| mommy10 | riddle list | $0757 | 8×14 | Mummy_10.png |  |
| mommy11 | riddle list | $078F | 8×14 | Mummy_11.png |  |
| mommy12 | riddle list | $07C7 | 8×14 | Mummy_12.png |  |
| daddy1 | riddle list | $082F | 10×13 | Daddy_1.png |  |
| daddy2 | riddle list | $0870 | 10×13 | Daddy_2.png |  |
| daddy3 | riddle list | $08B1 | 10×13 | Daddy_3.png |  |
| daddy4 | riddle list | $08F2 | 10×13 | Daddy_4.png |  |
| daddy5 | riddle list | $0933 | 10×13 | Daddy_5.png |  |
| daddy6 | riddle list | $0974 | 10×13 | Daddy_6.png |  |
| daddy7 | riddle list | $09B5 | 10×13 | Daddy_7.png |  |
| daddy8 | riddle list | $09F6 | 10×13 | Daddy_8.png |  |
| daddy9 | riddle list | $0A37 | 10×13 | Daddy_9.png |  |
| daddy10 | riddle list | $0A78 | 10×13 | Daddy_10.png |  |
| daddy11 | riddle list | $0AB9 | 10×13 | Daddy_11.png |  |
| daddy12 | riddle list | $0AFA | 10×13 | Daddy_12.png |  |
| mikey1 | riddle list | $0B6B | 6×11 | Mikey_1.png |  |
| mikey2 | riddle list | $0B8C | 6×11 | Mikey_2.png |  |
| mikey3 | riddle list | $0BAD | 6×11 | Mikey_3.png |  |
| mikey4 | riddle list | $0BCE | 6×11 | Mikey_4.png |  |
| mikey5 | riddle list | $0BEF | 6×11 | Mikey_5.png |  |
| mikey6 | riddle list | $0C10 | 6×11 | Mikey_6.png |  |
| mikey7 | riddle list | $0C31 | 6×11 | Mikey_7.png |  |
| mikey8 | riddle list | $0C52 | 6×11 | Mikey_8.png |  |
| mikey9 | riddle list | $0C73 | 6×11 | Mikey_9.png |  |
| mikey10 | riddle list | $0C94 | 6×11 | Mikey_10.png |  |
| mikey11 | riddle list | $0CB5 | 6×11 | Mikey_11.png |  |
| mikey12 | riddle list | $0CD6 | 6×11 | Mikey_12.png |  |
| hulk1 | riddle list | $0D1D | 14×16 | Hulk_1.png |  |
| hulk2 | riddle list | $0D8D | 14×16 | Hulk_2.png |  |
| hulk3 | riddle list | $0DFD | 14×16 | Hulk_3.png |  |
| hulk4 | riddle list | $0E6D | 14×16 | Hulk_4.png |  |
| hulk5 | riddle list | $0EDD | 14×16 | Hulk_5.png |  |
| hulk6 | riddle list | $0F4D | 14×16 | Hulk_6.png |  |
| hulk7 | riddle list | $0FBD | 14×16 | Hulk_7.png |  |
| hulk8 | riddle list | $102D | 14×16 | Hulk_8.png |  |
| hulk9 | riddle list | $109D | 14×16 | Hulk_9.png |  |
| sphereoid1 | riddle list | $1512 | 16×15 | Spheroid_1.png |  |
| sphereoid2 | riddle list | $158A | 16×15 | Spheroid_2.png |  |
| sphereoid3 | riddle list | $1602 | 16×15 | Spheroid_3.png |  |
| sphereoid4 | riddle list | $167A | 16×15 | Spheroid_4.png |  |
| sphereoid5 | riddle list | $16F2 | 16×15 | Spheroid_5.png |  |
| sphereoid6 | riddle list | $176A | 16×15 | Spheroid_6.png |  |
| sphereoid7 | riddle list | $17E2 | 16×15 | Spheroid_7.png |  |
| sphereoid8 | riddle list | $185A | 16×15 | Spheroid_8.png |  |
| enforcer1 | riddle list | $18EA | 10×11 | Enforcer_1.png |  |
| enforcer2 | riddle list | $1921 | 10×11 | Enforcer_2.png |  |
| enforcer3 | riddle list | $1958 | 10×11 | Enforcer_3.png |  |
| enforcer4 | riddle list | $198F | 10×11 | Enforcer_4.png |  |
| enforcer5 | riddle list | $19C6 | 10×11 | Enforcer_5.png |  |
| enforcer6 | riddle list | $19FD | 10×11 | Enforcer_6.png |  |
| enforcerbullet1 | riddle list | $1A44 | 8×7 | Spark_1.png |  |
| enforcerbullet2 | riddle list | $1A60 | 8×7 | Spark_2.png |  |
| enforcerbullet3 | riddle list | $1A7C | 8×7 | Spark_3.png |  |
| enforcerbullet4 | riddle list | $1A98 | 8×7 | Spark_4.png |  |
| player | riddle list | $1F6C | 12×16 | PlayerBig.png |  |
| brain1 | riddle list | $2171 | 14×16 | Brain_1.png |  |
| brain2 | riddle list | $21E1 | 14×16 | Brain_2.png |  |
| brain3 | riddle list | $2251 | 14×16 | Brain_3.png |  |
| brain4 | riddle list | $22C1 | 14×16 | Brain_4.png |  |
| brain5 | riddle list | $2331 | 14×16 | Brain_5.png |  |
| brain6 | riddle list | $23A1 | 14×16 | Brain_6.png |  |
| brain7 | riddle list | $2411 | 14×16 | Brain_7.png |  |
| brain8 | riddle list | $2481 | 14×16 | Brain_8.png |  |
| brain9 | riddle list | $24F1 | 14×16 | Brain_9.png |  |
| brain10 | riddle list | $2561 | 14×16 | Brain_10.png |  |
| brain11 | riddle list | $25D1 | 14×16 | Brain_11.png |  |
| brain12 | riddle list | $2641 | 14×16 | Brain_12.png |  |
| player1 | riddle list | $361B | 8×12 | Player_1.png |  |
| player2 | riddle list | $364B | 8×12 | Player_2.png |  |
| player3 | riddle list | $367B | 8×12 | Player_3.png |  |
| player4 | riddle list | $36AB | 8×12 | Player_4.png |  |
| player5 | riddle list | $36DB | 8×12 | Player_5.png |  |
| player6 | riddle list | $370B | 8×12 | Player_6.png |  |
| player7 | riddle list | $373B | 8×12 | Player_7.png |  |
| player8 | riddle list | $376B | 8×12 | Player_8.png |  |
| player9 | riddle list | $379B | 8×12 | Player_9.png |  |
| player10 | riddle list | $37CB | 8×12 | Player_10.png |  |
| player11 | riddle list | $37FB | 8×12 | Player_11.png |  |
| player12 | riddle list | $382B | 8×12 | Player_12.png |  |
| electrode1 | riddle list | $3B95 | 10×9 | Electrode_1.png |  |
| electrode2 | riddle list | $3BC2 | 10×9 | Electrode_2.png |  |
| electrode3 | riddle list | $3BEF | 10×9 | Electrode_3.png |  |
| electrode4 | riddle list | $3C1C | 10×9 | Electrode_4.png |  |
| electrode5 | riddle list | $3C49 | 10×9 | Electrode_5.png |  |
| electrode6 | riddle list | $3C76 | 10×9 | Electrode_6.png |  |
| electrode7 | riddle list | $3CA3 | 10×9 | Electrode_7.png |  |
| electrode8 | riddle list | $3CD0 | 10×9 | Electrode_8.png |  |
| electrode9 | riddle list | $3CFD | 10×9 | Electrode_9.png |  |
| electrode10 | riddle list | $3D2A | 10×9 | Electrode_10.png |  |
| electrode11 | riddle list | $3D57 | 10×9 | Electrode_11.png |  |
| electrode12 | riddle list | $3D84 | 10×9 | Electrode_12.png |  |
| electrode13 | riddle list | $3DB1 | 6×9 | Electrode_13.png |  |
| electrode14 | riddle list | $3DCC | 6×9 | Electrode_14.png |  |
| electrode15 | riddle list | $3DE7 | 6×9 | Electrode_15.png |  |
| electrode16 | riddle list | $3E02 | 10×9 | Electrode_16.png |  |
| electrode17 | riddle list | $3E2F | 10×9 | Electrode_17.png |  |
| electrode18 | riddle list | $3E5C | 10×9 | Electrode_18.png |  |
| electrode19 | riddle list | $3E89 | 18×7 | Electrode_19.png |  |
| electrode20 | riddle list | $3EC8 | 18×7 | Electrode_20.png |  |
| electrode21 | riddle list | $3F07 | 18×7 | Electrode_21.png |  |
| electrode22 | riddle list | $3F46 | 10×9 | Electrode_22.png |  |
| electrode23 | riddle list | $3F73 | 10×9 | Electrode_23.png |  |
| electrode24 | riddle list | $3FA0 | 10×9 | Electrode_24.png |  |
| electrode25 | riddle list | $3FCD | 10×10 | Electrode_25.png |  |
| electrode26 | riddle list | $3FFF | 10×10 | Electrode_26.png |  |
| electrode27 | riddle list | $4031 | 10×10 | Electrode_27.png |  |
| grunt1 | riddle list | $4073 | 10×13 | Grunt_1.png |  |
| grunt2 | riddle list | $40B4 | 10×13 | Grunt_2.png |  |
| grunt3 | riddle list | $40F5 | 10×13 | Grunt_3.png |  |
| quark1 | riddle list | $50E6 | 16×15 | Quark_1.png |  |
| quark2 | riddle list | $515E | 16×15 | Quark_2.png |  |
| quark3 | riddle list | $51D6 | 16×15 | Quark_3.png |  |
| quark4 | riddle list | $524E | 16×15 | Quark_4.png |  |
| quark5 | riddle list | $52C6 | 16×15 | Quark_5.png |  |
| quark6 | riddle list | $533E | 16×15 | Quark_6.png |  |
| quark7 | riddle list | $53B6 | 16×15 | Quark_7.png |  |
| quark8 | riddle list | $542E | 16×15 | Quark_8.png |  |
| quark9 | riddle list | $54A6 | 16×15 | Quark_9.png |  |
| tank1 | riddle list | $551E | 14×16 | Tank_1.png |  |
| tank2 | riddle list | $558E | 14×16 | Tank_2.png |  |
| tank3 | riddle list | $55FE | 14×16 | Tank_3.png |  |
| tank4 | riddle list | $566E | 14×16 | Tank_4.png |  |
| smallfont0 | riddle list | $EA2B | 4×5 | Font_S_0.png |  |
| smallfont1 | riddle list | $EA36 | 4×5 | Font_S_1.png |  |
| smallfont2 | riddle list | $EA41 | 4×5 | Font_S_2.png |  |
| smallfont3 | riddle list | $EA4C | 4×5 | Font_S_3.png |  |
| smallfont4 | riddle list | $EA57 | 4×5 | Font_S_4.png |  |
| smallfont5 | riddle list | $EA62 | 4×5 | Font_S_5.png |  |
| smallfont6 | riddle list | $EA6D | 4×5 | Font_S_6.png |  |
| smallfont7 | riddle list | $EA78 | 4×5 | Font_S_7.png |  |
| smallfont8 | riddle list | $EA83 | 4×5 | Font_S_8.png |  |
| smallfont9 | riddle list | $EA8E | 4×5 | Font_S_9.png |  |
| smallfontA | riddle list | $EAD7 | 4×5 | Font_S_A.png |  |
| smallfontB | riddle list | $EAE2 | 4×5 | Font_S_B.png |  |
| smallfontC | riddle list | $EAED | 4×5 | Font_S_C.png |  |
| smallfontD | riddle list | $EAF8 | 4×5 | Font_S_D.png |  |
| smallfontE | riddle list | $EB03 | 4×5 | Font_S_E.png |  |
| smallfontF | riddle list | $EB0E | 4×5 | Font_S_F.png |  |
| smallfontG | riddle list | $EB19 | 4×5 | Font_S_G.png |  |
| smallfontH | riddle list | $EB24 | 4×5 | Font_S_H.png |  |
| smallfontI | riddle list | $EB2F | 4×5 | Font_S_I.png |  |
| smallfontJ | riddle list | $EB3A | 4×5 | Font_S_J.png |  |
| smallfontK | riddle list | $EB45 | 4×5 | Font_S_K.png |  |
| smallfontL | riddle list | $EB50 | 4×5 | Font_S_L.png |  |
| smallfontM | riddle list | $EB5B | 6×5 | Font_S_M.png |  |
| smallfontN | riddle list | $EB6B | 4×5 | Font_S_N.png |  |
| smallfontO | riddle list | $EB76 | 4×5 | Font_S_O.png |  |
| smallfontP | riddle list | $EB81 | 4×5 | Font_S_P.png |  |
| smallfontQ | riddle list | $EB8C | 4×5 | Font_S_Q.png |  |
| smallfontR | riddle list | $EB97 | 4×5 | Font_S_R.png |  |
| smallfontS | riddle list | $EBA2 | 4×5 | Font_S_S.png |  |
| smallfontT | riddle list | $EBAD | 4×5 | Font_S_T.png |  |
| smallfontU | riddle list | $EBB8 | 4×5 | Font_S_U.png |  |
| smallfontV | riddle list | $EBC3 | 4×5 | Font_S_V.png |  |
| smallfontW | riddle list | $EBCE | 6×5 | Font_S_W.png |  |
| smallfontX | riddle list | $EBDE | 4×5 | Font_S_X.png |  |
| smallfontY | riddle list | $EBE9 | 4×5 | Font_S_Y.png |  |
| smallfontZ | riddle list | $EBF4 | 4×5 | Font_S_Z.png |  |
| smallfont( | riddle list | $EBFF | 4×5 | Font_S_(.png |  |
| smallfont) | riddle list | $EC0A | 4×5 | Font_S_).png |  |
| largefont0 | riddle list | $EC93 | 6×6 | Font_L_0.png |  |
| largefont1 | riddle list | $ECA6 | 6×6 | Font_L_1.png |  |
| largefont2 | riddle list | $ECB9 | 6×6 | Font_L_2.png |  |
| largefont3 | riddle list | $ECCC | 6×6 | Font_L_3.png |  |
| largefont4 | riddle list | $ECDF | 6×6 | Font_L_4.png |  |
| largefont5 | riddle list | $ECF2 | 6×6 | Font_L_5.png |  |
| largefont6 | riddle list | $ED05 | 6×6 | Font_L_6.png |  |
| largefont7 | riddle list | $ED18 | 6×6 | Font_L_7.png |  |
| largefont8 | riddle list | $ED2B | 6×6 | Font_L_8.png |  |
| largefont9 | riddle list | $ED3E | 6×6 | Font_L_9.png |  |
| largefontA | riddle list | $EDC0 | 6×6 | Font_L_A.png |  |
| largefontB | riddle list | $EDD3 | 6×6 | Font_L_B.png |  |
| largefontC | riddle list | $EDE6 | 6×6 | Font_L_C.png |  |
| largefontD | riddle list | $EDF9 | 6×6 | Font_L_D.png |  |
| largefontE | riddle list | $EE0C | 6×6 | Font_L_E.png |  |
| largefontF | riddle list | $EE1F | 6×6 | Font_L_F.png |  |
| largefontG | riddle list | $EE32 | 6×6 | Font_L_G.png |  |
| largefontH | riddle list | $EE45 | 6×6 | Font_L_H.png |  |
| largefontI | riddle list | $EE58 | 6×6 | Font_L_I.png |  |
| largefontJ | riddle list | $EE6B | 6×6 | Font_L_J.png |  |
| largefontK | riddle list | $EE7E | 6×6 | Font_L_K.png |  |
| largefontL | riddle list | $EE91 | 6×6 | Font_L_L.png |  |
| largefontM | riddle list | $EEA4 | 6×6 | Font_L_M.png |  |
| largefontN | riddle list | $EEB7 | 6×6 | Font_L_N.png |  |
| largefontO | riddle list | $EECA | 6×6 | Font_L_O.png |  |
| largefontP | riddle list | $EEDD | 6×6 | Font_L_P.png |  |
| largefontQ | riddle list | $EEF0 | 6×6 | Font_L_Q.png |  |
| largefontR | riddle list | $EF03 | 6×6 | Font_L_R.png |  |
| largefontS | riddle list | $EF16 | 6×6 | Font_L_S.png |  |
| largefontT | riddle list | $EF29 | 6×6 | Font_L_T.png |  |
| largefontU | riddle list | $EF3C | 6×6 | Font_L_U.png |  |
| largefontV | riddle list | $EF4F | 6×6 | Font_L_V.png |  |
| largefontW | riddle list | $EF62 | 6×6 | Font_L_W.png |  |
| largefontX | riddle list | $EF75 | 6×6 | Font_L_X.png |  |
| largefontY | riddle list | $EF88 | 6×6 | Font_L_Y.png |  |
| largefontZ | riddle list | $EF9B | 6×6 | Font_L_Z.png |  |
| largefont( | riddle list | $EFAE | 4×6 | Font_L_(.png |  |
| largefont) | riddle list | $EFBB | 4×6 | Font_L_).png |  |
| largefont: | riddle list | $EFC7 | 2×5 | Font_L_colon.png |  |
| largefontarrowleft | riddle list | $EFDB | 6×6 | Font_L_arrowleft.png |  |
| TankShell | rom chase | $4FF2 | 8×7 | TankShell.png | pass C  |
| TankGrow_1 | rom chase | $5036 | 4×4 | TankGrow_1.png | pass C  |
| TankGrow_2 | rom chase | $503E | 8×7 | TankGrow_2.png | pass C  |
| TankGrow_3 | rom chase | $505A | 8×8 | TankGrow_3.png | pass C  |
| TankGrow_4 | rom chase | $507A | 12×12 | TankGrow_4.png | pass C  |
| ProgBurst | rom chase | source data | 12×16 | ProgBurst.png | pass C solid blit |
| CMPIC | source RRB10 | $110D | 6×4 | MissileSmall_0.png | pass B (ROM-verified)  |
| CMP1 | source RRB10 | $206F | 6×4 | MissileSmall_1.png | pass B (ROM-verified)  |
| MNPIC | source RRG23 | $3596 | 6×8 | PlayerExtra.png | pass B (ROM-verified)  |
| NULLP | source RRH11 | $045D | 4×2 | Dot.png | pass B (ROM-verified)  |
| CRUSB | source RRSCRIPT | $86BA | 18×2 | AttractCruise.png | pass B (ROM-verified)  |
