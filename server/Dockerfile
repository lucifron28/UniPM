FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["global.json", "."]
COPY ["server/server.csproj", "server/"]
RUN dotnet restore "server/server.csproj"

COPY ["server/", "server/"]
WORKDIR /src/server
RUN dotnet publish "server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "UniPM.Api.dll"]
