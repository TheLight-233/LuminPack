'use strict';

// Serve the actual Unity build and require the Player's explicit success marker.
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');

const root = path.resolve(process.argv[2] || '');
const modulePath = path.resolve(process.env.LUMINPACK_PLAYWRIGHT || '.tmp/webgl-ci/node_modules/playwright-core');
const { chromium } = require(modulePath);
const timeoutMs = Number(process.env.LUMINPACK_WEBGL_TIMEOUT_MS || 180000);
if (!fs.existsSync(path.join(root, 'index.html')) || !Number.isFinite(timeoutMs) || timeoutMs <= 0)
    throw new Error('A WebGL build directory and positive timeout are required.');

const contentTypes = { '.html': 'text/html', '.js': 'application/javascript', '.wasm': 'application/wasm', '.json': 'application/json', '.data': 'application/octet-stream' };
const server = http.createServer((request, response) => {
    let file;
    try {
        const pathname = decodeURIComponent(new URL(request.url, 'http://localhost').pathname);
        file = path.resolve(root, '.' + (pathname === '/' ? '/index.html' : pathname));
        if (file !== root && !file.startsWith(root + path.sep)) throw new Error('Invalid path');
    } catch {
        response.writeHead(400).end();
        return;
    }
    fs.readFile(file, (error, bytes) => {
        if (error) { response.writeHead(404).end(); return; }
        response.writeHead(200, { 'Content-Type': contentTypes[path.extname(file)] || 'application/octet-stream' });
        response.end(bytes);
    });
});

(async () => {
    let browser;
    let timer;
    try {
        await new Promise((resolve, reject) => { server.once('error', reject); server.listen(0, '127.0.0.1', resolve); });
        browser = await chromium.launch({
            executablePath: process.env.LUMINPACK_WEBGL_BROWSER || '/usr/bin/google-chrome',
            headless: true,
            args: ['--no-sandbox', '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'],
        });
        const page = await browser.newPage();
        const result = new Promise((resolve, reject) => {
            page.on('console', message => {
                const text = message.text();
                if (text.includes('LUMINPACK_UNITY_SMOKE_') || message.type() === 'error') console.log(text);
                if (text.includes('LUMINPACK_UNITY_SMOKE_FAILED')) reject(new Error('The WebGL smoke test failed.'));
                else if (text.includes('LUMINPACK_UNITY_SMOKE_PASSED')) resolve();
            });
            page.on('pageerror', error => reject(error));
            timer = setTimeout(() => reject(new Error('The WebGL smoke test timed out.')), timeoutMs);
        });
        console.log('Browser: ' + browser.version());
        await Promise.all([page.goto(`http://127.0.0.1:${server.address().port}/`, { waitUntil: 'load', timeout: timeoutMs }), result]);
        console.log('WebGL Player completed successfully.');
    } catch (error) {
        console.error(error);
        process.exitCode = 1;
    } finally {
        clearTimeout(timer);
        if (browser) await browser.close();
        server.close();
    }
})();
