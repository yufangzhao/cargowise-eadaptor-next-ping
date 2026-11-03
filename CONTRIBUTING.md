# Contributing

Thanks for considering a contribution! This project deliberately stays tiny - please keep it that way.

## Ground rules

- One page, one purpose: configure OAuth 2.0 client-certificate auth, send Universal XML, read the response.
  Features beyond that scope are out of scope for this repo.
- .NET 8, ASP.NET Core minimal API, single static HTML page with vanilla JavaScript.
  **Zero NuGet packages and no front-end build chain** - if a change needs a package or npm, it does not belong in this repo.
- UI language and code comments in English; keep comments minimal.
- Theme values live as CSS variables at the top of `wwwroot/css/site.css` - never hard-code colors.

## Building

```
dotnet build src/EadaptorNextPing/EadaptorNextPing.csproj
```

## Pull requests

1. Keep the change focused; one feature or fix per PR.
2. Describe what you changed and why.
3. Do not commit private keys, certificates or `App_Data/`.

## Reporting issues

Include: OS, .NET version, steps to reproduce, expected vs actual behaviour, and the console output
(redact tokens, tenant IDs and endpoints if needed).
