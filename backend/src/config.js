require('dotenv').config();

module.exports = {
  port: process.env.PORT || 8081,
  jwtSecret: process.env.JWT_SECRET || 'default-dev-secret',
  platformSecret: process.env.PLATFORM_SECRET || 'default-platform-dev-secret',
  platformApiKey: process.env.PLATFORM_API_KEY || 'dev-platform-api-key',
  xertraRpcUrl: process.env.XERTRA_RPC_URL || 'https://rpc.xertra.com',
  burnAddress: '0x0000000000000000000000000000000000000000',
  burnAmount: '1000000000000000000', // 1 STRAX in wei
  maxFreePlays: 3,
  web3auth: {
    jwksUrl: 'https://api-auth.web3auth.io/jwks',
  },
};
