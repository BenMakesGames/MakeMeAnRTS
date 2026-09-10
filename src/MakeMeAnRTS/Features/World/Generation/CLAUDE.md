# World generation

The steps in `WorldGenerator.TryGenerate` run in an order that is load-bearing. Reordering them
produces maps that look plausible and are subtly broken, so the dependencies are worth knowing before
moving anything.

1. **Elevation and moisture** (`NoiseField`) are normalised to span 0..1. Every threshold downstream -
   water level, rock level, forest density - assumes that, or the same setting floods one seed and
   leaves the next bone dry.
2. **Terrain painting** reads the raw elevation. It must happen *before* sink filling, so that lakes are
   real depressions rather than the epsilon-filled basins the filler leaves behind.
3. **Sink filling** (`SinkFiller`) seeds from the map edge *and* from the water painted in step 2, which
   is what lets rivers drain into lakes instead of only off the edge. Run it before terrain painting and
   there is no water to drain into.
4. **Rivers** (`RiverCarver`) need step 3. Steepest descent on raw noise stops at the first local
   minimum, which is everywhere; that produced stub rivers and diamond-shaped ponds where the
   pit-flooding fallback fired. `RiverOutcome.Stalled` counts how often that still happens - it should
   be zero, and `map-preview` prints the tally.
5. **Shoreline** is collected first and applied second. Painting sand in place lets sand seed more sand.
6. **Bridges** run in two passes for different reasons. `BridgeBuilder.Build` scatters crossings for
   flavour; `ConnectRegions` runs *after* start positions are chosen and adds whatever is needed to make
   them mutually reachable, because which crossings matter depends on where players start.
7. **Start sites** are chosen before `PrepareSite` levels them, and `PrepareSite` only clears trees and
   breaks up rock - it will not fill water, so `StartSiteChooser` rejects any site with water nearby
   rather than leaving a base half-drowned.

Generation is allowed to fail. Some seeds have nowhere fair to start; `Generate` retries on a derived
seed and finally throws rather than handing back an unplayable map. `map-preview` prints the seed it
actually used, which will differ from the one asked for when a retry happened.
