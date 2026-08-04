FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY OmniArchivum.Api.csproj .
RUN dotnet restore OmniArchivum.Api.csproj

COPY . .
RUN dotnet publish OmniArchivum.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OmniArchivum.Api.dll"]
