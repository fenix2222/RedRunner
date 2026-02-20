mergeInto(LibraryManager.library, {

    Web3Auth_Init: function (clientIdPtr, chainIdPtr, rpcTargetPtr, blockExplorerUrlPtr, chainDisplayNamePtr, tickerNamePtr, gameObjectNamePtr) {
        var clientId = UTF8ToString(clientIdPtr);
        var chainId = UTF8ToString(chainIdPtr);
        var rpcTarget = UTF8ToString(rpcTargetPtr);
        var blockExplorerUrl = UTF8ToString(blockExplorerUrlPtr);
        var chainDisplayName = UTF8ToString(chainDisplayNamePtr);
        var tickerName = UTF8ToString(tickerNamePtr);
        var gameObjectName = UTF8ToString(gameObjectNamePtr);

        window._web3authUnityObject = gameObjectName;

        function sendToUnity(method, data) {
            if (window.unityInstance) {
                window.unityInstance.SendMessage(window._web3authUnityObject, method, data || '');
            }
        }

        window._web3authSendToUnity = sendToUnity;

        try {
            var chainConfig = {
                chainNamespace: 'eip155',
                chainId: chainId,
                rpcTarget: rpcTarget,
                displayName: chainDisplayName,
                blockExplorerUrl: blockExplorerUrl,
                ticker: tickerName,
                tickerName: tickerName,
                decimals: 18
            };

            var privateKeyProvider = new EthereumProvider.EthereumPrivateKeyProvider({
                config: { chainConfig: chainConfig }
            });

            window._web3auth = new Modal.Web3Auth({
                clientId: clientId,
                web3AuthNetwork: 'sapphire_devnet',
                chainConfig: chainConfig,
                privateKeyProvider: privateKeyProvider,
                uiConfig: {
                    appName: 'Xertra Passport',
                    theme: { primary: '#38023b' },
                    mode: 'dark',
                    logoDark: 'https://stratispherestaging.blob.core.windows.net/images/Xertra_Logo_White_Transparent.png',
                    logoLight: 'https://stratispherestaging.blob.core.windows.net/images/Xertra_Logo_Transparent.png',
                    defaultLanguage: 'en',
                    loginGridCol: 3,
                    primaryButton: 'externalLogin'
                }
            });

            // Configure external wallet adapters (MetaMask, WalletConnect, etc.)
            if (typeof DefaultEvmAdapter !== 'undefined' && DefaultEvmAdapter.getDefaultExternalAdapters) {
                try {
                    var adapters = DefaultEvmAdapter.getDefaultExternalAdapters({ options: window._web3auth.options });
                    adapters.forEach(function (adapter) {
                        window._web3auth.configureAdapter(adapter);
                    });
                    console.log('[Web3Auth] Configured ' + adapters.length + ' external wallet adapters');
                } catch (adapterErr) {
                    console.warn('[Web3Auth] Failed to configure external adapters:', adapterErr);
                }
            } else {
                console.warn('[Web3Auth] DefaultEvmAdapter not loaded, external wallets will not be available');
            }

            window._web3auth.initModal().then(function () {
                sendToUnity('OnInitComplete', '');
            }).catch(function (err) {
                sendToUnity('OnInitFailed', err.message || 'Init failed');
            });

        } catch (err) {
            sendToUnity('OnInitFailed', err.message || 'Init exception');
        }
    },

    Web3Auth_Login: function () {
        var sendToUnity = window._web3authSendToUnity;
        var web3auth = window._web3auth;

        if (!web3auth) {
            sendToUnity('OnLoginFailed', 'Web3Auth not initialized');
            return;
        }

        web3auth.connect().then(function (provider) {
            return Promise.all([
                provider.request({ method: 'eth_requestAccounts' }),
                web3auth.getUserInfo(),
                web3auth.authenticateUser()
            ]);
        }).then(function (results) {
            var accounts = results[0];
            var userInfo = results[1];
            var authInfo = results[2];

            var data = {
                walletAddress: accounts[0] || '',
                email: userInfo.email || '',
                name: userInfo.name || '',
                profileImage: userInfo.profileImage || '',
                idToken: authInfo.idToken || '',
                appJwtToken: ''
            };

            sendToUnity('OnLoginSuccess', JSON.stringify(data));
        }).catch(function (err) {
            sendToUnity('OnLoginFailed', err.message || 'Login failed');
        });
    },

    Web3Auth_Logout: function () {
        var sendToUnity = window._web3authSendToUnity;
        var web3auth = window._web3auth;

        if (!web3auth) {
            sendToUnity('OnLogoutComplete', '');
            return;
        }

        web3auth.logout().then(function () {
            sendToUnity('OnLogoutComplete', '');
        }).catch(function (err) {
            console.warn('Web3Auth logout error:', err);
            sendToUnity('OnLogoutComplete', '');
        });
    },

    Web3Auth_CheckSession: function () {
        var sendToUnity = window._web3authSendToUnity;
        var web3auth = window._web3auth;

        if (!web3auth || !web3auth.connected) {
            sendToUnity('OnNoSession', '');
            return;
        }

        Promise.all([
            web3auth.provider.request({ method: 'eth_requestAccounts' }),
            web3auth.getUserInfo(),
            web3auth.authenticateUser()
        ]).then(function (results) {
            var accounts = results[0];
            var userInfo = results[1];
            var authInfo = results[2];

            var data = {
                walletAddress: accounts[0] || '',
                email: userInfo.email || '',
                name: userInfo.name || '',
                profileImage: userInfo.profileImage || '',
                idToken: authInfo.idToken || '',
                appJwtToken: ''
            };

            sendToUnity('OnSessionFound', JSON.stringify(data));
        }).catch(function (err) {
            console.warn('Web3Auth session check error:', err);
            sendToUnity('OnNoSession', '');
        });
    }
});
