# Aill Gameplay Suite

Unified Bannerlord v1.4.7 source project replacing the six legacy assemblies with one controlled runtime.

This branch is an engineering baseline, not a final release. It intentionally refuses unsafe legacy combat patches and does not invoke private issue consequence methods. Every module is isolated behind guarded execution, bounded processing budgets and persisted state.

## Build

```powershell
dotnet restore src/AillGameplaySuite/AillGameplaySuite.csproj
dotnet build src/AillGameplaySuite/AillGameplaySuite.csproj -c Release --no-restore
```

The install package contains one runtime assembly: `AillGameplaySuite.dll`.
