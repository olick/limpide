cd ~/dev/limpide

# 1. Nouveau mot de passe sans caractère spécial
sed -i "s/^POSTGRES_PASSWORD=.*/POSTGRES_PASSWORD=$(openssl rand -hex 24)/" .env

# 2. Limiter les ports à la machine locale
sed -i 's/- "\${POSTGRES_PORT/- "127.0.0.1:${POSTGRES_PORT/; s/- "\${OLLAMA_PORT/- "127.0.0.1:${OLLAMA_PORT/' docker-compose.yml
grep 127.0.0.1 docker-compose.yml
# doit afficher 2 lignes :
#   - "127.0.0.1:${POSTGRES_PORT:-5432}:5432"
#   - "127.0.0.1:${OLLAMA_PORT:-11434}:11434"

# 3. Repartir de zéro : PostgreSQL ne lit le mot de passe qu'à la création du volume
docker compose down -v
docker compose up -d --wait

# 4. Vérifier
docker compose config > /dev/null && echo "config OK"   # plus aucun WARN
docker compose ps                                        # PORTS : 127.0.0.1:...
docker compose exec postgres psql -U rag -d rag -c "\dt"