## UDP-based networking library with Protobuf serialization
This is a small personal experiment in low-level networking, primarily for learning purposes. I also wanted to explore alternative high-level architecture approaches compared to what I’ve used in UNET and Photon. The library is built to work both as a standalone C# library (for independent apps) and as a Unity library.

This repository includes implementation of common delivery protcols (see [Channels](./Networking/Broadcasting/Channels)):
- Unreliable
- Unreliable Sequenced
- Reliable
- Reliable Sequenced
- Reliable Fragmented

There is a basic RPC layer used by the demos and the matchmaking server.
On top of that, there is an high-level “framework” design based on self-synchronizing data entities.

This repository contains some test coverage for the delivery protocols (see [Tests](./Tests)) and a small console C# app with a matchmaking implementation used in the demos (see [MatchMakingServer](./MatchMakingServer)).

Demo videos (real prototypes using this library):  
https://www.youtube.com/watch?v=2RFL9UODowE  
https://www.youtube.com/watch?v=vcZ-CErBQlU