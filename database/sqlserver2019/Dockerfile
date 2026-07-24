FROM mcr.microsoft.com/mssql/server:2019-latest

USER root

# SQL Server 2019 Linux packages use the Ubuntu 20.04 Microsoft repository.
# The package check makes a missing Full-Text Search installation fail the image build.
RUN apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends ca-certificates curl gnupg \
    && curl -fsSL https://packages.microsoft.com/keys/microsoft.asc \
        | gpg --batch --yes --dearmor -o /usr/share/keyrings/microsoft-prod.gpg \
    && curl -fsSL https://packages.microsoft.com/config/ubuntu/20.04/mssql-server-2019.list \
        -o /etc/apt/sources.list.d/mssql-server-2019.list \
    && apt-get update \
    && ACCEPT_EULA=Y DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends mssql-server-fts \
    && dpkg-query --show mssql-server-fts \
    && rm -rf /var/lib/apt/lists/*

USER mssql
