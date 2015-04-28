BlueRain
=====

Managed, fully featured memory manipulation library written in (mostly) C# providing an easy to use API.

BlueRain is essentially a continuation of Hyperion, and is currently the only open-source part of the xRain framework.

Main project goals:
* Provide a safe, easy to use API for common memory manipulation requirements
* Reasonable performance (1)
* Support for both internal and external memory operations
* Provide a concise, testable core library for third party developers
* Maintain a thoroughly documented codebase 

(1) - Reasonable in this scope is defined as "good performance within idiomatic C#". This means that when the choice can be made between a hack that isn't idiomatic C#, but is faster, and an idiomatic C# solution, the idiomatic solution will be preferred.

License
=====

BlueRain is licensed under the very permissive Apache 2.0 license. Any submodules or dependencies may be under different licenses.

API
=====

The main API of BlueRain is exposed through two types:
* `ExternalProcessMemory` for external process manipulation - can be used on any process
* `InternalProcessMemory` for internal (injected) process manipulation - requires the CLR to be present 

Both implement the `NativeMemory` base class, and provide various generic `Read<T>` and `Write<T>` methods, as well as implementation-specific other members.

Most of the API is IntelliSense documented - IntelliSense XML can be generated on build.

Tests
=====

BlueRain uses MSTest for its unit tests. These are contained in a stand-alone BlueRain.Tests solution. While the aim is to attain 100% coverage, coverage should never fall below 75%.

Contributing
=====
Pull requests are very welcome, as long as they are accompanied by an issue with a clear description of what problem the PR is addressing. Please be as detailed as possible in your issue reports.
