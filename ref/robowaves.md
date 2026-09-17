# Robotron enemies by wave

Source: https://seanriddle.com/robowaves.html (fetched 2026-08-30). Cross-reference for wave
spawning — extract authoritative values from the ROM tables below (D-014); this page decodes them.

## ROM locations
- **$2B7C** — subroutine that sets up game-play parameters for each wave. Pulls unique data for the
  first 40 waves, then **repeats waves 21–40** as needed.
- **$2E24** — table of enemy counts: number of Grunts for waves 1–40, then Electrodes (waves 1–40),
  then Mommies, etc. (9 entity columns).
- **$2C12** — table of ~12 other game-play parameters (timing values, possibly max shot counts).
  Values are modified by the difficulty setting. **9 parameters generally decrease** as the wave
  number increases (game speeds up the longer you play), **3 generally increase**.

## Wave-number behaviour
- Displayed as 2 decimal digits; wave after 99 displays as **0** (difficulty stays high).
- Wave number stored as **8 bits**; after wave **255** (displayed as 55) the game returns to wave 1
  and difficulty resets (like a new game).
- Long game: 1 [easy] → 99 [hard] → 0 (100) [hard] → 99 (199) [hard] → 0 (200) [hard] → 55 (255)
  [hard] → 1 [easy].

## Enemy counts per wave (waves 1–40)
Wave	Grunts	Electrodes	Mommies	Daddies	Mikeys	Hulks	Brains	Spheroids	Quarks
1	15	5	1	1	0	0	0	0	0
2	17	15	1	1	1	5	0	1	0
3	22	25	2	2	2	6	0	3	0
4	34	25	2	2	2	7	0	4	0
5	20	20	15	0	1	0	15	1	0
6	32	25	3	3	3	7	0	4	0
7	0	0	4	4	4	12	0	0	10
8	35	25	3	3	3	8	0	5	0
9	60	0	3	3	3	4	0	5	0
10	25	20	0	22	0	0	20	1	0
11	35	25	3	3	3	8	0	5	0
12	0	0	3	3	3	13	0	0	12
13	35	25	3	3	3	8	0	5	0
14	27	5	5	5	5	20	0	2	0
15	25	20	0	0	22	2	20	1	0
16	35	25	3	3	3	3	0	5	0
17	0	0	3	3	3	14	0	0	12
18	35	25	3	3	3	8	0	5	0
19	70	0	3	3	3	3	0	5	0
20	25	20	8	8	8	2	20	2	0
21	35	25	3	3	3	8	0	5	0
22	0	0	3	3	3	15	0	0	12
23	35	25	3	3	3	8	0	5	0
24	0	0	3	3	3	13	0	6	7
25	25	20	25	0	1	1	21	1	0
26	35	25	3	3	3	8	0	5	0
27	0	0	3	3	3	16	0	0	12
28	35	25	3	3	3	8	0	5	1
29	75	0	3	3	3	4	0	5	1
30	25	20	0	25	0	1	22	1	1
31	35	25	3	3	3	8	0	5	1
32	0	0	3	3	3	16	0	0	13
33	35	25	3	3	3	8	0	5	1
34	30	0	3	3	3	25	0	2	2
35	27	15	0	0	25	2	23	1	2
36	35	25	3	3	3	8	0	5	2
37	0	0	3	3	3	16	0	0	14
38	35	25	3	3	3	8	0	5	2
39	80	0	3	3	3	6	0	5	1
40	30	15	10	10	10	2	25	1	1
									
