#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# deploy.sh — one-command deploy for Ubuntu EC2
#
# Usage:
#   chmod +x deploy.sh
#   ./deploy.sh
#
# Prerequisites:
#   - Docker and docker-compose-plugin installed on the VM
#   - .env file created in the same directory as this script
# ─────────────────────────────────────────────────────────────────────────────
set -euo pipefail

COMPOSE_FILE="docker-compose.yml"

echo ""
echo "══════════════════════════════════════════"
echo "  Bradford Council Chatbot — Deploy"
echo "══════════════════════════════════════════"
echo ""

# ── Guard: .env must exist ────────────────────────────────────────────────────
if [ ! -f ".env" ]; then
  echo "❌  .env file not found."
  echo "    Create it from .env.example:"
  echo ""
  echo "      cp .env.example .env"
  echo "      nano .env   # add your OPENAI_API_KEY"
  echo ""
  exit 1
fi

# ── Guard: OPENAI_API_KEY must be set ─────────────────────────────────────────
source .env 2>/dev/null || true
if [ -z "${OPENAI_API_KEY:-}" ] || [ "$OPENAI_API_KEY" = "sk-proj-REPLACE_WITH_YOUR_KEY" ]; then
  echo "❌  OPENAI_API_KEY is not set or still a placeholder in .env"
  exit 1
fi

echo "✅  .env loaded"
echo ""

# ── Stop any running containers ───────────────────────────────────────────────
echo "⏹   Stopping existing containers..."
docker compose -f "$COMPOSE_FILE" down --remove-orphans 2>/dev/null || true

# ── Build images ──────────────────────────────────────────────────────────────
echo ""
echo "🔨  Building images (this takes a few minutes on first run)..."
docker compose -f "$COMPOSE_FILE" build

# ── Start containers ──────────────────────────────────────────────────────────
echo ""
echo "🚀  Starting containers..."
docker compose -f "$COMPOSE_FILE" up -d

# ── Prune old images ──────────────────────────────────────────────────────────
echo ""
echo "🧹  Pruning dangling images..."
docker image prune -f

# ── Status ────────────────────────────────────────────────────────────────────
echo ""
echo "📋  Container status:"
docker compose -f "$COMPOSE_FILE" ps

echo ""
echo "══════════════════════════════════════════"
echo "  Waiting for health checks..."
echo "  Services can take 3-5 minutes to become"
echo "  healthy on first boot."
echo ""
echo "  Check status with:"
echo "    docker compose ps"
echo ""
echo "  View logs with:"
echo "    docker compose logs -f"
echo ""
echo "  Access the chatbot at:"
echo "    http://$(curl -s ifconfig.me 2>/dev/null || echo 'YOUR_EC2_IP'):8080"
echo "══════════════════════════════════════════"
echo ""
