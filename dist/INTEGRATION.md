# Sharingway IPC Framework - Integration Guide

This distribution contains pre-built libraries and headers for easy integration.

## Contents

- `csharp/` - .NET library (Sharingway.Net.dll)
- `cpp/` - C++ headers and static library
- `docs/` - Documentation and guides

## Quick Integration

### C# Projects

1. Copy `csharp/Sharingway.Net.dll` to your project
2. Add reference in your .csproj:

```xml
<ItemGroup>
  <Reference Include="Sharingway.Net">
    <HintPath>path\to\Sharingway.Net.dll</HintPath>
  </Reference>
</ItemGroup>
```

### C++ Projects

1. Copy `cpp/include/` headers to your include path
2. Copy `cpp/lib/x64/Sharingway.native.lib` to your library path
3. Link against Sharingway.native.lib

See docs/README.md for detailed integration instructions.
