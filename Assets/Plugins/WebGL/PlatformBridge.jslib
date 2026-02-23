mergeInto(LibraryManager.library, {

    PlatformBridge_StartListening: function (gameObjectNamePtr) {
        var goName = UTF8ToString(gameObjectNamePtr);
        window._platformBridgeObject = goName;

        function sendToUnity(method, data) {
            if (window.unityInstance) {
                window.unityInstance.SendMessage(window._platformBridgeObject, method, data || '');
            }
        }

        // Check if a session was buffered before Unity loaded
        if (window._pendingPlatformSession) {
            console.log('[PlatformBridge] Found buffered session, forwarding to Unity');
            sendToUnity('OnPlatformSessionReceived', JSON.stringify(window._pendingPlatformSession));
            window._pendingPlatformSession = null;
            return;
        }

        // Listen for future INIT_SESSION messages from parent
        window.addEventListener('message', function (event) {
            if (event.data && event.data.type === 'INIT_SESSION') {
                console.log('[PlatformBridge] Received INIT_SESSION from parent');
                sendToUnity('OnPlatformSessionReceived', JSON.stringify(event.data));
            }
        });

        // Tell parent we are ready to receive session
        if (window.parent !== window) {
            window.parent.postMessage({ type: 'GAME_READY' }, '*');
            console.log('[PlatformBridge] Sent GAME_READY to parent');
        }
    },

    PlatformBridge_SendToParent: function (jsonPtr) {
        var json = UTF8ToString(jsonPtr);
        if (window.parent !== window) {
            try {
                window.parent.postMessage(JSON.parse(json), '*');
            } catch (e) {
                console.warn('[PlatformBridge] Failed to send to parent:', e);
            }
        }
    }
});
