FROM node:22-alpine AS client
WORKDIR /src/Buscarr.Client
COPY Buscarr.Client/package.json Buscarr.Client/package-lock.json ./
RUN npm ci
COPY Buscarr.Client/ ./
RUN npm run build -- --outDir dist --emptyOutDir

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS server
WORKDIR /src
COPY Buscarr.Server/ Buscarr.Server/
RUN dotnet publish Buscarr.Server/Buscarr.Server.csproj -c Release -o /app -p:SkipClient=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=server /app ./
COPY --from=client /src/Buscarr.Client/dist ./wwwroot
ENV ASPNETCORE_URLS=http://0.0.0.0:3000
USER $APP_UID
EXPOSE 3000
ENTRYPOINT ["dotnet", "Buscarr.Server.dll"]
