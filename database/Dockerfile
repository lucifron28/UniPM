FROM mcr.microsoft.com/mssql/server:2025-latest

USER root

RUN apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends ca-certificates curl gnupg \
    && curl -fsSL https://packages.microsoft.com/keys/microsoft.asc \
        | gpg --batch --yes --dearmor -o /usr/share/keyrings/microsoft-prod.gpg \
    && curl -fsSL https://packages.microsoft.com/config/ubuntu/24.04/mssql-server-2025.list \
        -o /etc/apt/sources.list.d/mssql-server-2025.list \
    && apt-get update \
    && ACCEPT_EULA=Y DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends mssql-server-fts \
    && rm -rf /var/lib/apt/lists/*

USER mssql
