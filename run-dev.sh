#!/bin/bash
# Starts both the backend (port 8081) and the test platform app (port 8082)

cleanup() {
    echo ""
    echo "Shutting down..."
    kill $BACKEND_PID $PLATFORM_PID 2>/dev/null
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

echo ""
echo "Backend:        http://localhost:8081"
echo "Test Platform:  http://localhost:8082"
echo ""
echo "Press Ctrl+C to stop both."

wait
