module.exports = {
  '/api': { target: process.env.SMS_API_URL || 'http://localhost:8080', secure: true, changeOrigin: true }
};
