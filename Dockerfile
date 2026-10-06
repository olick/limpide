# Images de Limpide, construites depuis la racine du dépôt :
#   docker build --target web -t limpide-web .
#   docker build --target ingestion -t limpide-ingestion .
# Aucun secret dans les images : connexion, clé Mistral, etc. arrivent par variables d'environnement.

# --- Compilation (commune aux deux images) ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restauration d'abord, à partir des seuls .csproj : la couche reste en cache tant que les dépendances ne changent pas.
COPY src/Limpide.Core/Limpide.Core.csproj src/Limpide.Core/
COPY src/Limpide.Infrastructure/Limpide.Infrastructure.csproj src/Limpide.Infrastructure/
COPY src/Limpide.Ingestion/Limpide.Ingestion.csproj src/Limpide.Ingestion/
COPY src/Limpide.Web/Limpide.Web.csproj src/Limpide.Web/
RUN dotnet restore src/Limpide.Web/Limpide.Web.csproj \
 && dotnet restore src/Limpide.Ingestion/Limpide.Ingestion.csproj

COPY src/ src/
# Pas de --no-restore : la restauration précédente ne voyait que les .csproj ; or le SDK n'ajoute le paquet des
# fichiers Blazor (Microsoft.AspNetCore.App.Internal.Assets, dont _framework/blazor.web.js) qu'en voyant les
# composants .razor. Sans nouvelle restauration, l'image publiée n'a pas blazor.web.js (404 constaté le 2026-10-06).
RUN dotnet publish src/Limpide.Web/Limpide.Web.csproj -c Release -o /out/web \
 && dotnet publish src/Limpide.Ingestion/Limpide.Ingestion.csproj -c Release -o /out/ingestion \
 && test -f /out/web/wwwroot/_framework/blazor.web.js

# --- Application web ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS web
WORKDIR /app
COPY --from=build /out/web .
ENV ASPNETCORE_HTTP_PORTS=8080 \
    # Derrière le reverse proxy : lire X-Forwarded-For / X-Forwarded-Proto (adresse du visiteur, HTTPS).
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
EXPOSE 8080
# Utilisateur non root fourni par l'image Microsoft.
USER $APP_UID
ENTRYPOINT ["dotnet", "Limpide.Web.dll"]

# --- Console d'ingestion (fetch, extract, chunk, embed, evaluate...) ---
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS ingestion
WORKDIR /app
COPY --from=build /out/ingestion .
# Les chemins de la configuration (corpus.json, eval/, data/) sont relatifs au répertoire de travail.
WORKDIR /limpide
COPY corpus.json ./
COPY eval/questions.json eval/
RUN mkdir -p data eval/results && chown -R $APP_UID /limpide
USER $APP_UID
ENTRYPOINT ["dotnet", "/app/Limpide.Ingestion.dll"]
