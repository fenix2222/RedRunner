require('dotenv').config();

module.exports = {
  port: process.env.PORT || 3001,
  jwtSecret: process.env.JWT_SECRET || 'default-dev-secret',
  web3auth: {
    jwksUrl: 'https://api-auth.web3auth.io/jwks',
  },
};
