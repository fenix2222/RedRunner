const config = require('../config');
const db = require('../db');

const checkBurnUsed = db.prepare('SELECT id FROM burn_transactions WHERE tx_hash = ?');
const insertBurnTx = db.prepare('INSERT INTO burn_transactions (tx_hash, wallet_address) VALUES (?, ?)');

async function rpcCall(method, params) {
  const res = await fetch(config.xertraRpcUrl, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ jsonrpc: '2.0', id: 1, method, params }),
  });
  const json = await res.json();
  if (json.error) throw new Error(json.error.message);
  return json.result;
}

async function verifyBurn(txHash, expectedSender) {
  // Check replay
  const existing = checkBurnUsed.get(txHash);
  if (existing) return { valid: false, error: 'Burn transaction already used' };

  try {
    // Get transaction details
    const tx = await rpcCall('eth_getTransactionByHash', [txHash]);
    if (!tx) return { valid: false, error: 'Transaction not found' };

    // Get receipt to confirm it succeeded
    const receipt = await rpcCall('eth_getTransactionReceipt', [txHash]);
    if (!receipt) return { valid: false, error: 'Transaction receipt not found' };

    // Verify status (0x1 = success)
    if (receipt.status !== '0x1') return { valid: false, error: 'Transaction failed' };

    // Verify recipient is burn address
    if (tx.to && tx.to.toLowerCase() !== config.burnAddress.toLowerCase()) {
      return { valid: false, error: 'Transaction not sent to burn address' };
    }

    // Verify amount >= 1 STRAX
    const value = BigInt(tx.value);
    const required = BigInt(config.burnAmount);
    if (value < required) return { valid: false, error: 'Insufficient burn amount' };

    // Verify sender
    if (tx.from.toLowerCase() !== expectedSender.toLowerCase()) {
      return { valid: false, error: 'Transaction sender does not match wallet' };
    }

    // Record to prevent replay
    insertBurnTx.run(txHash, expectedSender.toLowerCase());

    return { valid: true };
  } catch (err) {
    return { valid: false, error: 'Burn verification failed: ' + err.message };
  }
}

module.exports = { verifyBurn };
