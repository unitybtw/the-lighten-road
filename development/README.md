# Development workspace

Use personal folders for isolated C# prototypes, technical notes, and experiments before integration into the shared Unity project. These folders are not executable Unity projects and are not imported by Unity automatically. The proposed feature ownership is documented in the [developer task split](../docs/DEVELOPMENT_PLAN.md); treat it as a draft until both developers review it.

| Folder | Lead |
| --- | --- |
| [sirac/](sirac/) | Siraç — @unitybtw |
| [kemal/](kemal/) | Kemal — @kemal-berke |

Coordinate the owner of each system before coding it. Agreed runtime code belongs in `Game/Assets/_Project/Scripts/`. Move accepted prototypes there rather than maintaining duplicate implementations. See [the collaboration guide](../docs/COLLABORATION.md).
