# LogistFlow — Module 01: Foundations

Module 01 is a .NET 10 console app for registering package collections, finding them by status or priority, and updating their status by ID. The operational summary shows total collections, total packages, and collection counts for every priority and status.

## Run

In Visual Studio with .NET 10 support, open `LogistFlow.slnx` from the repository root and press **Ctrl+F5**. From a terminal at the repository root, run:

```sh
dotnet run --project LogistFlow/LogistFlow.csproj
```

Choose **1** to register a customer name, collection address, positive package count, and priority. The app assigns a numeric ID and sets the status to `Pending`. Choose **5** to update the status by ID, **6** for the summary, or **7** to exit.

## Local data

The app saves each successful registration and status update to `data/collections.json` in its working directory. The command above uses the `LogistFlow` project directory. Use the same working directory when launching the compiled executable directly to reopen the same records.

The first run starts with an empty list. Each new ID is one greater than the highest saved ID. One session at a time can open a data directory. If the JSON is damaged, the app exits and preserves the file for recovery. A failed save leaves the last saved records available.

## Verify

Run these commands from the repository root in PowerShell. The smoke test uses disposable data under `LogistFlow/obj`.

```powershell
dotnet build LogistFlow.slnx --configuration Release
./LogistFlow/smoke.ps1
```
