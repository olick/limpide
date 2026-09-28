#!/usr/bin/env bash
# Crée la solution .NET de la phase 1. À lancer une seule fois, depuis la racine du dépôt.
set -euo pipefail

if [[ -d src ]] && [[ -n "$(ls -A src 2>/dev/null)" ]]; then
  echo "src/ n'est pas vide : bootstrap déjà fait." >&2
  exit 1
fi

dotnet new sln -n Limpide

# Cœur : modèle du domaine, découpage, interfaces. Aucune dépendance d'infrastructure.
dotnet new classlib -n Limpide.Core      -o src/Limpide.Core      -f net10.0
# Ingestion : collecte, extraction, embeddings, écriture en base (application console).
dotnet new console  -n Limpide.Ingestion -o src/Limpide.Ingestion -f net10.0
# Tests du cœur (le découpage surtout).
dotnet new xunit    -n Limpide.Core.Tests -o tests/Limpide.Core.Tests -f net10.0

dotnet sln add src/Limpide.Core src/Limpide.Ingestion tests/Limpide.Core.Tests

dotnet add src/Limpide.Ingestion reference src/Limpide.Core
dotnet add tests/Limpide.Core.Tests reference src/Limpide.Core

# Abstractions IA (IEmbeddingGenerator) : le cœur ne dépend pas d'Ollama.
dotnet add src/Limpide.Core package Microsoft.Extensions.AI.Abstractions

dotnet add src/Limpide.Ingestion package Microsoft.Extensions.Hosting
dotnet add src/Limpide.Ingestion package Npgsql
dotnet add src/Limpide.Ingestion package Pgvector
dotnet add src/Limpide.Ingestion package AngleSharp   # extraction HTML
dotnet add src/Limpide.Ingestion package PdfPig       # extraction PDF
dotnet add src/Limpide.Ingestion package OllamaSharp  # implémente IEmbeddingGenerator

rm -f src/Limpide.Core/Class1.cs tests/Limpide.Core.Tests/UnitTest1.cs

dotnet build
echo "Solution prête."
