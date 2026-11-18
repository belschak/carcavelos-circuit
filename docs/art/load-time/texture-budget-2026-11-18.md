# Texture budget, Web build (P8, 18 Nov 2026)

Source: Texture2D objects read from CI Web build 44316930456 (`world-<md5>.bundle` = campus world, game `.data` = everything else). Bytes = Unity's image size incl. mips (crunched textures are already compressed; gzip barely shrinks them).
Rule: downscale only when the largest size the object ever covers on a 1920x1080 screen is at most half the texture size. Textures under 100 KB are summed per group.

Result (CI Web build 44342020870, PR #166): the 12 `TyreFar_*` maps are 128 px and the 10 film grain maps are gone in both the DXT and the ETC2 `.data` (texture bytes in the game 21.2 -> 14.6 MB raw); the game download is 24.49 -> 22.68 MB gzip. The grain maps were referenced twice (URP global settings and the renderers' PostProcessData), both are redirected for the Web build only.

| Texture | Where | Size | Format | KB | Decision | Why |
|---|---|---|---|---|---|---|
| Unbounded-Black-SDF Atlas 0 | game | 1024x1024 | Alpha8 | 1024 | keep | font atlas: text drawn at full size in menus |
| BarlowCondensed-ExtraBold-SDF Atlas 0 | game | 1024x1024 | Alpha8 | 1024 | keep | font atlas: text drawn at full size in menus |
| ArchivoExpanded-ExtraBoldItalic-SDF Atlas 0 | game | 1024x1024 | Alpha8 | 1024 | keep | font atlas: text drawn at full size in menus |
| logo-word | game | 1200x160 | RGBA32 | 750 | keep | small (< 100 KB) or UI |
| Char_Tiago_BaseColor | game | 1024x1024 | DXT1 | 682 | keep 1024 | driver fills ~600 px in the driver select close-up |
| Char_Rookie_BaseColor | game | 1024x1024 | DXT1 | 682 | keep 1024 | driver fills ~600 px in the driver select close-up |
| Char_Nova_BaseColor | game | 1024x1024 | DXT1 | 682 | keep 1024 | driver fills ~600 px in the driver select close-up |
| Char_Amara_BaseColor | game | 1024x1024 | DXT1 | 682 | keep 1024 | driver fills ~600 px in the driver select close-up |
| TyreFar_Retro_Normal | game | 512x512 | DXT5 | 341 | 512 -> 128 | far kart only under 12 % of screen height: tyre <= ~45 px at 1080p |
| TyreFar_Retro_MetalSmooth | game | 512x512 | DXT5 | 341 | 512 -> 128 | far kart only under 12 % of screen height: tyre <= ~45 px at 1080p |
| TyreFar_Offroad_Normal | game | 512x512 | DXT5 | 341 | 512 -> 128 | far kart only under 12 % of screen height: tyre <= ~45 px at 1080p |
| TyreFar_Offroad_MetalSmooth | game | 512x512 | DXT5 | 341 | 512 -> 128 | far kart only under 12 % of screen height: tyre <= ~45 px at 1080p |
| TyreFar_Normal | game | 512x512 | DXT5 | 341 | 512 -> 128 | far kart only under 12 % of screen height: tyre <= ~45 px at 1080p |
| TyreFar_MetalSmooth | game | 512x512 | DXT5 | 341 | 512 -> 128 | far kart only under 12 % of screen height: tyre <= ~45 px at 1080p |
| TyreFar_Balloon_Normal | game | 512x512 | DXT5 | 341 | 512 -> 128 | far kart only under 12 % of screen height: tyre <= ~45 px at 1080p |
| TyreFar_Balloon_MetalSmooth | game | 512x512 | DXT5 | 341 | 512 -> 128 | far kart only under 12 % of screen height: tyre <= ~45 px at 1080p |
| FX_Smoke_4x4 | game | 512x512 | DXT5 | 341 | keep | small (< 100 KB) or UI |
| Char_Tiago_Normal | game | 512x512 | DXT5 | 341 | keep 512 | driver close-up in the driver select |
| Char_Rookie_Normal | game | 512x512 | DXT5 | 341 | keep 512 | driver close-up in the driver select |
| Char_Nova_Normal | game | 512x512 | DXT5 | 341 | keep 512 | driver close-up in the driver select |
| Char_Amara_Normal | game | 512x512 | DXT5 | 341 | keep 512 | driver close-up in the driver select |
| AreaTex | game | 160x560 | RGB24 | 262 | keep | small (< 100 KB) or UI |
| Thin02 | game | 512x512 | Alpha8 | 256 | left out of the Web build | URP film grain noise, grain never drawn on the Web (Web quality level, intensity 0) |
| Thin01 | game | 512x512 | Alpha8 | 256 | left out of the Web build | URP film grain noise, grain never drawn on the Web (Web quality level, intensity 0) |
| SeagullDelivery | game | 256x256 | RGBA32 | 256 | keep | small (< 100 KB) or UI |
| Medium06 | game | 512x512 | Alpha8 | 256 | left out of the Web build | URP film grain noise, grain never drawn on the Web (Web quality level, intensity 0) |
| Medium05 | game | 512x512 | Alpha8 | 256 | left out of the Web build | URP film grain noise, grain never drawn on the Web (Web quality level, intensity 0) |
| Medium04 | game | 512x512 | Alpha8 | 256 | left out of the Web build | URP film grain noise, grain never drawn on the Web (Web quality level, intensity 0) |
| Medium03 | game | 512x512 | Alpha8 | 256 | left out of the Web build | URP film grain noise, grain never drawn on the Web (Web quality level, intensity 0) |
| Medium02 | game | 512x512 | Alpha8 | 256 | left out of the Web build | URP film grain noise, grain never drawn on the Web (Web quality level, intensity 0) |
| Medium01 | game | 512x512 | Alpha8 | 256 | left out of the Web build | URP film grain noise, grain never drawn on the Web (Web quality level, intensity 0) |
| Large02 | game | 512x512 | Alpha8 | 256 | left out of the Web build | URP film grain noise, grain never drawn on the Web (Web quality level, intensity 0) |
| Large01 | game | 512x512 | Alpha8 | 256 | left out of the Web build | URP film grain noise, grain never drawn on the Web (Web quality level, intensity 0) |
| Espresso | game | 256x256 | RGBA32 | 256 | keep | small (< 100 KB) or UI |
| DeansList | game | 256x256 | RGBA32 | 256 | keep | small (< 100 KB) or UI |
| CarcavelosWave | game | 256x256 | RGBA32 | 256 | keep | small (< 100 KB) or UI |
| item-frame | game | 224x224 | RGBA32 | 196 | keep | small (< 100 KB) or UI |
| m021_head_wetsuit | game | 512x512 | DXT1 | 170 | keep | small (< 100 KB) or UI |
| m021_body_wetsuit | game | 512x512 | DXT1 | 170 | keep | small (< 100 KB) or UI |
| f021_head_wetsuit | game | 512x512 | DXT1 | 170 | keep | small (< 100 KB) or UI |
| f021_body_wetsuit | game | 512x512 | DXT1 | 170 | keep | small (< 100 KB) or UI |
| TyreFar_Retro_Colour | game | 512x512 | DXT1 | 170 | 512 -> 128 | far kart only under 12 % of screen height: tyre <= ~45 px at 1080p |
| TyreFar_Offroad_Colour | game | 512x512 | DXT1 | 170 | 512 -> 128 | far kart only under 12 % of screen height: tyre <= ~45 px at 1080p |
| TyreFar_Colour | game | 512x512 | DXT1 | 170 | 512 -> 128 | far kart only under 12 % of screen height: tyre <= ~45 px at 1080p |
| TyreFar_Balloon_Colour | game | 512x512 | DXT1 | 170 | 512 -> 128 | far kart only under 12 % of screen height: tyre <= ~45 px at 1080p |
| Item_Tassel_base | game | 512x512 | DXT1 | 170 | keep | small (< 100 KB) or UI |
| Item_Seagull_base | game | 512x512 | DXT1 | 170 | keep | small (< 100 KB) or UI |
| Item_SeagullWing_base | game | 512x512 | DXT1 | 170 | keep | small (< 100 KB) or UI |
| Item_Mortarboard_base | game | 512x512 | DXT1 | 170 | keep | small (< 100 KB) or UI |
| Item_Lifebuoy_base | game | 512x512 | DXT1 | 170 | keep | small (< 100 KB) or UI |
| Item_Laurel_base | game | 512x512 | DXT1 | 170 | keep | small (< 100 KB) or UI |
| logo-ribbon | game | 384x68 | RGBA32 | 102 | keep | small (< 100 KB) or UI |
| ground_detail_normal_data | world | 1024x1024 | DXT5 | 1365 | keep | ground blend data map; crunch/downscale shows as blotches right in front of the kart (WebTextureCrunch rule) |
| ground_detail_data | world | 1024x1024 | DXT5 | 1365 | keep | ground blend data map; crunch/downscale shows as blotches right in front of the kart (WebTextureCrunch rule) |
| ground_colour_near | world | 2048x2048 | DXT5 crunched | 903 | keep | ground colour under the camera, already crunched |
| ground_mask_data | world | 1024x512 | DXT5 | 682 | keep | ground blend data map; crunch/downscale shows as blotches right in front of the kart (WebTextureCrunch rule) |
| sea_shore_data | world | 1024x1024 | DXT1 | 682 | keep | ground blend data map; crunch/downscale shows as blotches right in front of the kart (WebTextureCrunch rule) |
| ground_colour | world | 2048x1024 | DXT5 crunched | 477 | keep | ground colour under the camera, already crunched |
| veg_leaves_cutout_albedo | world | 512x512 | DXT5 | 341 | keep | small (< 100 KB) or UI |
| TrackProps | world | 256x256 | RGB24 | 255 | keep | small (< 100 KB) or UI |
| entrance_calcada_albedo | world | 1024x1024 | DXT1 crunched | 200 | keep 1024 | road, plaza and wall surfaces next to the road, crunched q50 |
| street_setts_albedo | world | 1024x1024 | DXT1 crunched | 191 | keep 1024 | road, plaza and wall surfaces next to the road, crunched q50 |
| facade_white_albedo | world | 1024x1024 | DXT1 crunched | 173 | keep 1024 | road, plaza and wall surfaces next to the road, crunched q50 |
| asphalt_albedo | world | 1024x1024 | DXT1 crunched | 172 | keep 1024 | road, plaza and wall surfaces next to the road, crunched q50 |
| CheckpointBanner | world | 256x1024 | DXT1 | 170 | keep | small (< 100 KB) or UI |
| street_brick_albedo | world | 1024x1024 | DXT1 crunched | 169 | keep 1024 | road, plaza and wall surfaces next to the road, crunched q50 |
| entrance_planks_albedo | world | 1024x1024 | DXT1 crunched | 163 | keep 1024 | road, plaza and wall surfaces next to the road, crunched q50 |
| north_buff_albedo | world | 1024x1024 | DXT1 crunched | 163 | keep 1024 | road, plaza and wall surfaces next to the road, crunched q50 |
| coast_concrete_pale_albedo | world | 1024x1024 | DXT1 crunched | 162 | keep 1024 | road, plaza and wall surfaces next to the road, crunched q50 |
| coast_concrete_beige_albedo | world | 1024x1024 | DXT1 crunched | 157 | keep 1024 | road, plaza and wall surfaces next to the road, crunched q50 |
| street_slab_dark_albedo | world | 1024x1024 | DXT1 crunched | 156 | keep 1024 | road, plaza and wall surfaces next to the road, crunched q50 |
| coast_concrete_ochre_albedo | world | 1024x1024 | DXT1 crunched | 155 | keep 1024 | road, plaza and wall surfaces next to the road, crunched q50 |
| street_slab_albedo | world | 1024x1024 | DXT1 crunched | 154 | keep 1024 | road, plaza and wall surfaces next to the road, crunched q50 |
| concrete_panel_albedo | world | 1024x1024 | DXT1 crunched | 146 | keep 1024 | road, plaza and wall surfaces next to the road, crunched q50 |
| street_concrete_albedo | world | 1024x1024 | DXT1 crunched | 146 | keep 1024 | road, plaza and wall surfaces next to the road, crunched q50 |
| plaza_ochre_albedo | world | 1024x1024 | DXT1 crunched | 144 | keep 1024 | road, plaza and wall surfaces next to the road, crunched q50 |
| campus_east_concrete_albedo | world | 1024x1024 | DXT1 crunched | 144 | keep 1024 | road, plaza and wall surfaces next to the road, crunched q50 |
| calcada_albedo | world | 1024x1024 | DXT1 crunched | 142 | keep 1024 | road, plaza and wall surfaces next to the road, crunched q50 |
| coast_trim_albedo | world | 1024x1024 | DXT1 crunched | 141 | keep 1024 | road, plaza and wall surfaces next to the road, crunched q50 |
| concrete_polished_albedo | world | 1024x1024 | DXT1 crunched | 137 | keep 1024 | road, plaza and wall surfaces next to the road, crunched q50 |
| concrete_sand_albedo | world | 1024x1024 | DXT1 crunched | 137 | keep 1024 | road, plaza and wall surfaces next to the road, crunched q50 |
| calcada_normal | world | 512x512 | DXT5 crunched | 122 | keep 512 | road/ground/wall normals already capped at 512 (WebTextureCrunch), seen up close |
| C3_suv_normal | world | 512x512 | DXT5 crunched | 120 | keep 512 | big roadside props, a car side spans ~700 px when passed at 5 m (V24 budget) |
| entrance_calcada_normal | world | 512x512 | DXT5 crunched | 120 | keep 512 | road/ground/wall normals already capped at 512 (WebTextureCrunch), seen up close |
| street_setts_normal | world | 512x512 | DXT5 crunched | 119 | keep 512 | road/ground/wall normals already capped at 512 (WebTextureCrunch), seen up close |
| street_setts_dark_normal | world | 512x512 | DXT5 crunched | 118 | keep 512 | road/ground/wall normals already capped at 512 (WebTextureCrunch), seen up close |
| facade_white_normal | world | 512x512 | DXT5 crunched | 116 | keep 512 | road/ground/wall normals already capped at 512 (WebTextureCrunch), seen up close |
| C5_microcar_normal | world | 512x512 | DXT5 crunched | 115 | keep 512 | big roadside props, a car side spans ~700 px when passed at 5 m (V24 budget) |
| asphalt_normal | world | 512x512 | DXT5 crunched | 114 | keep 512 | road/ground/wall normals already capped at 512 (WebTextureCrunch), seen up close |
| coast_atlas_albedo | world | 1024x1024 | DXT1 crunched | 114 | keep 1024 | road, plaza and wall surfaces next to the road, crunched q50 |
| C4_camper_normal | world | 512x512 | DXT5 crunched | 114 | keep 512 | big roadside props, a car side spans ~700 px when passed at 5 m (V24 budget) |
| S3_foodtruck_normal | world | 512x512 | DXT5 crunched | 113 | keep 512 | big roadside props, a car side spans ~700 px when passed at 5 m (V24 budget) |
| C2_estate_normal | world | 512x512 | DXT5 crunched | 112 | keep 512 | big roadside props, a car side spans ~700 px when passed at 5 m (V24 budget) |
| C1_hatchback_normal | world | 512x512 | DXT5 crunched | 110 | keep 512 | big roadside props, a car side spans ~700 px when passed at 5 m (V24 budget) |
| street_slab_dark_normal | world | 512x512 | DXT5 crunched | 110 | keep 512 | road/ground/wall normals already capped at 512 (WebTextureCrunch), seen up close |
| S4_kiosk_normal | world | 512x512 | DXT5 crunched | 109 | keep 512 | big roadside props, a car side spans ~700 px when passed at 5 m (V24 budget) |
| C6_panelvan_normal | world | 512x512 | DXT5 crunched | 109 | keep 512 | big roadside props, a car side spans ~700 px when passed at 5 m (V24 budget) |
| B3_lifeguard_normal | world | 512x512 | DXT5 crunched | 109 | keep 512 | big roadside props, a car side spans ~700 px when passed at 5 m (V24 budget) |
| street_brick_normal | world | 512x512 | DXT5 crunched | 109 | keep 512 | road/ground/wall normals already capped at 512 (WebTextureCrunch), seen up close |
| street_slab_normal | world | 512x512 | DXT5 crunched | 105 | keep 512 | road/ground/wall normals already capped at 512 (WebTextureCrunch), seen up close |
| concrete_panel_normal | world | 512x512 | DXT5 crunched | 105 | keep 512 | road/ground/wall normals already capped at 512 (WebTextureCrunch), seen up close |
| mural_original_albedo | world | 2048x512 | DXT1 crunched | 104 | keep 1024 | road, plaza and wall surfaces next to the road, crunched q50 |
| entrance_planks_normal | world | 512x512 | DXT5 crunched | 100 | keep 512 | road/ground/wall normals already capped at 512 (WebTextureCrunch), seen up close |
| coast_concrete_beige_normal | world | 512x512 | DXT5 crunched | 99 | keep 512 | road/ground/wall normals already capped at 512 (WebTextureCrunch), seen up close |
| coast_concrete_pale_normal | world | 512x512 | DXT5 crunched | 98 | keep 512 | road/ground/wall normals already capped at 512 (WebTextureCrunch), seen up close |
| coast_concrete_ochre_normal | world | 512x512 | DXT5 crunched | 98 | keep 512 | road/ground/wall normals already capped at 512 (WebTextureCrunch), seen up close |
| sea_waves_normal | world | 512x512 | DXT5 crunched | 98 | keep 512 | road/ground/wall normals already capped at 512 (WebTextureCrunch), seen up close |

| Group (each < 100 KB) | Count | KB |
|---|---|---|
| game: keep | 138 | 2522 |
| world: keep | 38 | 987 |
| world: keep 1024 | 4 | 103 |
| world: keep 256 | 20 | 488 |
| world: keep 512 | 16 | 647 |
