#!/bin/bash
# ─────────────────────────────────────────────────────────────────────────────
# generate-certs.sh  —  Generate a self-signed TLS certificate for the nginx
#                        reverse proxy.
#
# Run this ONCE on the EC2 host before starting the Docker stack:
#
#   bash Infrastructure/generate-certs.sh
#
# The script detects the server's public IP automatically and adds it as a
# Subject Alternative Name so Chrome / mobile browsers accept the cert
# (SAN is required; CN alone is ignored by modern browsers).
#
# For production with a real domain, replace with a Let's Encrypt cert
# (see the note at the bottom of this file).
# ─────────────────────────────────────────────────────────────────────────────

set -e

CERT_DIR="$(dirname "$0")/nginx/certs"
mkdir -p "$CERT_DIR"

# ── Detect public IP of this machine (used as SAN) ───────────────────────────
PUBLIC_IP=$(curl -s --max-time 5 https://api.ipify.org 2>/dev/null || \
            curl -s --max-time 5 http://checkip.amazonaws.com 2>/dev/null || \
            echo "")

SAN="IP:127.0.0.1,DNS:localhost"
if [ -n "$PUBLIC_IP" ]; then
  SAN="$SAN,IP:$PUBLIC_IP"
  echo "Detected public IP: $PUBLIC_IP"
fi

echo "Generating self-signed certificate..."
echo "  Subject Alternative Names: $SAN"
echo "  Output directory: $CERT_DIR"

# ── Generate cert ─────────────────────────────────────────────────────────────
openssl req -x509 -nodes -days 825 -newkey rsa:2048 \
  -keyout "$CERT_DIR/key.pem" \
  -out    "$CERT_DIR/cert.pem" \
  -subj "/C=GB/ST=West Yorkshire/L=Bradford/O=Bradford Council Prototype/CN=bradfordcouncil.local" \
  -addext "subjectAltName=$SAN"

chmod 600 "$CERT_DIR/key.pem"
chmod 644 "$CERT_DIR/cert.pem"

echo ""
echo "✓ Certificate generated:"
echo "  $CERT_DIR/cert.pem"
echo "  $CERT_DIR/key.pem"
echo ""
echo "Certificate details:"
openssl x509 -in "$CERT_DIR/cert.pem" -noout -subject -issuer -dates -ext subjectAltName
echo ""
echo "─────────────────────────────────────────────────────────────────────────"
echo "NEXT STEPS:"
echo "  1. Start the stack:  docker compose -f Infrastructure/docker-compose.yml up -d"
echo "  2. Open https://<your-ec2-ip> in your browser"
echo "  3. Browser will warn 'Not secure' — this is expected for self-signed certs."
echo "     On Chrome: click 'Advanced' → 'Proceed to <ip> (unsafe)'"
echo "     On mobile Chrome: same flow"
echo "  4. Microphone will work once you accept the certificate."
echo ""
echo "FOR PRODUCTION (Let's Encrypt with a real domain):"
echo "  See the comments at the bottom of this script."
echo "─────────────────────────────────────────────────────────────────────────"

# ─────────────────────────────────────────────────────────────────────────────
# TO USE LET'S ENCRYPT (free, trusted cert — requires a domain name):
#
#   1. Point your domain's A record to the EC2 public IP.
#   2. Ensure port 80 is open in the EC2 security group (needed for ACME challenge).
#   3. Install certbot on the EC2 host:
#        sudo apt install certbot
#   4. Obtain a certificate (replace example.com with your domain):
#        sudo certbot certonly --standalone -d example.com
#   5. Copy the certs to Infrastructure/nginx/certs/:
#        sudo cp /etc/letsencrypt/live/example.com/fullchain.pem Infrastructure/nginx/certs/cert.pem
#        sudo cp /etc/letsencrypt/live/example.com/privkey.pem   Infrastructure/nginx/certs/key.pem
#        sudo chown $USER:$USER Infrastructure/nginx/certs/*.pem
#   6. Restart nginx:  docker compose restart nginx
#   7. Set up auto-renewal (certbot renews every 60 days automatically via a cron
#      job; you'll also need to copy updated certs and restart nginx after each renewal).
# ─────────────────────────────────────────────────────────────────────────────
