# Route waypoints (Noah's adjusted route, T2)

Source: Noah's sketch (green line on the campus map, axes in metres, same origin as `tools/world_gen`: x = east, z = north, (0, 0) = lat 38.6780, lon -9.3265).
Read off the sketch by eye (about +/- 5 m). To be checked against the walk videos and corrected in Unity.
Driving direction: from the start towards the east, first into the building.

| # | x | z | Section | Notes |
|---|---|---|---|---|
| 0 | -17 | 132 | start / finish | north of the Student Center, black bar on the sketch |
| 1 | 18 | 135 | atrium | small loop, then into the building, covered |
| 2 | 32 | 122 | atrium | covered |
| 3 | 49 | 90 | atrium | covered |
| 4 | 49 | 67 | atrium | covered |
| 5 | 32 | 61 | atrium exit | covered |
| 6 | 5 | 58 | take-off | west side of the Academic Hall |
| 7 | -19 | 58 | take-off | end of the building, jump starts here |
| 8 | -94 | 38 | landing | flight arrow points west |
| 9 | -96 | 23 | landing | |
| 10 | -96 | -18 | road west side | south along the Marginal side |
| 11 | -101 | -41 | roundabout | |
| 12 | -132 | -50 | west loop | |
| 13 | -187 | -38 | west loop | |
| 14 | -241 | -18 | west loop | turning point |
| 15 | -252 | -28 | west loop | |
| 16 | -241 | -38 | beach side | |
| 17 | -200 | -76 | beach side | |
| 18 | -173 | -103 | beach side | |
| 19 | -132 | -137 | beach side | |
| 20 | -118 | -157 | sand | sand patch, beach stays as Luc built it |
| 21 | -91 | -154 | promenade | |
| 22 | -60 | -140 | promenade | |
| 23 | -57 | -124 | tunnel plaza | |
| 24 | -29 | -123 | tunnel plaza | |
| 25 | 18 | -106 | tunnel | under the Avenida Marginal |
| 26 | 53 | -93 | tunnel exit | |
| 27 | 94 | -72 | ramp up | |
| 28 | 114 | -41 | ramp up | |
| 29 | 118 | -24 | stairs | drivable, narrow steps with light rumble |
| 30 | 107 | -4 | stairs | |
| 31 | 97 | 16 | stairs | |
| 32 | 94 | 44 | east side | north along the east side |
| 33 | 73 | 64 | east side | |
| 34 | 66 | 108 | east side | short canopy (covered) around z 98-150 |
| 35 | 73 | 149 | east side | |
| 36 | 80 | 180 | north | |
| 37 | 46 | 188 | north | |
| 38 | 5 | 183 | north | |
| 39 | -23 | 176 | north | |
| 40 | -33 | 152 | north | |
| 41 | -16 | 135 | back to start | closes the loop at #0 |

## Open points
- Heights are not set yet: they come from `route_profile.json` in the generator (tunnel floor, upper level 14.5 m, terrace edge).
- The take-off at #6/#7 replaces the terrace-roof jump in Luc's route; the landing (#8/#9) is on the road side near x -95.
- Drivable stairs (#29-#31): rumble is a bump effect on the kart, not real steps in the mesh (to decide with Role 1).
