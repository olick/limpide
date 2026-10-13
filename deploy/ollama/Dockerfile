# Ollama avec bge-m3 inclus dans l'image (S6, Azure Container Apps) : un environnement recréé à chaque séance n'a
# rien à télécharger au démarrage, et toutes les instances ont exactement le même modèle (ID 790764642607, le même
# que sur le poste et le VPS, vérifié le 2026-10-13).
# Le VPS n'en a pas besoin (volume persistant + service ollama-model, docker-compose.prod.yml).
# Depuis l'ADR-012 (2026-10-13), Azure vectorise par OVHcloud AI Endpoints : cette image ne sert plus qu'en secours
# (deploy/publier-images.sh --avec-ollama).

FROM ollama/ollama:0.34.4 AS modele
# Démarre le serveur le temps de télécharger le modèle, puis l'arrête : le modèle reste dans /root/.ollama.
RUN ollama serve & pid=$! \
 && until ollama list >/dev/null 2>&1; do sleep 1; done \
 && ollama pull bge-m3 \
 && kill "$pid"
# Bibliothèques GPU (CUDA, MLX, Vulkan) : ≈ 4,7 Go inutiles sur Container Apps, qui tourne sur CPU.
RUN rm -rf /usr/lib/ollama/cuda_* /usr/lib/ollama/mlx_* /usr/lib/ollama/vulkan

# Image finale sur la même base (Ubuntu 24.04), sans les couches GPU : 11,5 Go → 2,4 Go (vecteurs identiques à ceux
# du poste, écart maximal 0,0, vérifié le 2026-10-13).
FROM ubuntu:24.04
COPY --from=modele /usr/bin/ollama /usr/bin/ollama
COPY --from=modele /usr/lib/ollama /usr/lib/ollama
COPY --from=modele /root/.ollama /root/.ollama
ENV OLLAMA_HOST=0.0.0.0:11434
EXPOSE 11434
ENTRYPOINT ["/usr/bin/ollama"]
CMD ["serve"]
