# Contributing

Contributions are welcome through pull requests.

1. Keep the SDK receive-only; sending and console management do not belong
   in this package.
2. Keep `Runtime/Core/`, `Contract/`, `Events/`, `Stub/`, `Facade/` and `Migration/`
   free of `UnityEngine` references and C# 9-compatible (Unity 2022.3 floor). Add
   tests for behavior changes and run `dotnet test "Tests~/OpenPush.Bridge.Tests.csproj"`
   (plus `OpenPush.Compat.Tests.csproj` and `OpenPush.Editor.Tests.csproj` when you
   touch those areas).
3. Do not commit credentials, provider configuration, local paths, build
   output, or customer data.
4. Use Conventional Commits for commit subjects.

First-time external workflow runs may require maintainer approval.
