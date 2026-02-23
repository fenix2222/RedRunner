#!/bin/bash
# Starts backend (8081), game (8082), and test platform app (8083)

cleanup() {
    echo ""
    echo "Shutting down..."
    kill $BACKEND_PID $GAME_PID $PLATFORM_PID 2>/dev/null
    exit 0
}

trap cleanup SIGINT SIGTERM

DIR="$(cd "$(dirname "$0")" && pwd)"

# Start backend
echo "Starting backend on http://localhost:8081 ..."
cd "$DIR/backend" && npm start &
BACKEND_PID=$!

# Start test platform app
echo "Starting test platform on http://localhost:8082 ..."
cd "$DIR/platform-test" && npx serve -l 8082 -s &
PLATFORM_PID=$!

# Start game (WebGL build)
echo "Starting game on http://localhost:8083 ..."
npx http-server "$DIR/Web" -p 8083 -c-1 --silent &
GAME_PID=$!

echo ""
echo "Backend:        http://localhost:8081"
echo "Test Platform:  http://localhost:8082"
echo "Game:           http://localhost:8083"
echo ""
echo "Press Ctrl+C to stop all."

wait
