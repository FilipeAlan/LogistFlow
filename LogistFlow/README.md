# LogistFlow console guide

Module 02 extends the original console with Brazilian postal address lookup. The [repository README](../README.md) covers the project layout and build and test commands. The [Module 01 release](https://github.com/FilipeAlan/LogistFlow/tree/v1.0.0) preserves the original console and its instructions.

## Run

In Visual Studio with .NET 10 support, open `LogistFlow.slnx` from the repository root and press **Ctrl+F5**. From a terminal at the repository root, run:

```sh
dotnet run --project LogistFlow/LogistFlow.csproj
```

Choose **1** and enter the customer name and CEP. Review the address from ViaCEP, complete missing details, and enter a positive package count and priority. If the lookup fails, type the address manually. The app assigns a numeric ID and sets the status to `Pending`.

Choose **2** for the full list, **3** or **4** for a status or priority filter, **5** to update a status by ID, **6** for the summary across all collections, or **7** to exit.

## Local data

The app saves each successful registration and status update to `data/collections.json` in its working directory. The command above uses the `LogistFlow` project directory. Use the same working directory when launching the compiled executable directly to reopen the same records.

The first run starts with an empty list. Each new ID is one greater than the highest saved ID. One session at a time can open a data directory. If the JSON is damaged, the app exits and preserves the file for recovery. A failed save leaves the last saved records available.

## Verify

Run these commands from the repository root in PowerShell. The smoke test uses disposable data under `LogistFlow/obj`.

```powershell
dotnet build LogistFlow.slnx --configuration Release
dotnet test LogistFlow.slnx --configuration Release --no-build
./LogistFlow/smoke.ps1
```
