FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
COPY app/ ./
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "UniPM.Api.dll"]
