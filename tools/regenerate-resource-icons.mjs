#!/usr/bin/env node
/**
 * Regenerate resource icons with transparent backgrounds.
 * Strategy: generate on plain white bg → Runware bg removal → clean transparent PNG.
 */

import { randomUUID } from 'crypto';
import { writeFile, mkdir } from 'fs/promises';
import { existsSync } from 'fs';
import path from 'path';

const API_URL = 'https://api.runware.ai/v1';
const API_KEY = process.env.RUNWARE_API_KEY || 'mrkrkANAo0C2Cvi1w8UZqn95JK64ngJc';
const OUT_DIR = path.join(import.meta.dirname, '..', 'game', 'Assets', 'Art', 'UI', 'Production');
const MODEL = 'runware:100@1';

const ICONS = [
    {
        id: 'icon_grain',
        prompt: 'single golden wheat sheaf bundle game icon, ripe grain tied with brown twine, warm harvest gold tones, simple clean mobile game resource icon, centered on plain white background',
    },
    {
        id: 'icon_iron',
        prompt: 'single iron ingot metal bar game icon, polished dark steel with slight blue sheen, forged metal resource, simple clean mobile game resource icon, centered on plain white background',
    },
    {
        id: 'icon_stone',
        prompt: 'single carved stone block game icon, rough hewn gray granite with chisel marks, stacked masonry resource, simple clean mobile game resource icon, centered on plain white background',
    },
    {
        id: 'icon_arcane',
        prompt: 'single glowing arcane energy orb game icon, swirling violet and teal magical sphere, mystical essence resource, simple clean mobile game resource icon, centered on plain white background',
    },
    {
        id: 'icon_gems',
        prompt: 'single faceted purple crystal gem game icon, brilliant amethyst gemstone with sparkle, premium currency diamond, simple clean mobile game resource icon, centered on plain white background',
    },
];

async function generateImage(prompt) {
    const taskUUID = randomUUID();
    const fullPrompt = 'vibrant mobile game UI icon art, bright saturated colors, clean sharp edges, ' + prompt + ', high detail, professional game asset, isolated on white';

    const body = [{
        taskType: 'imageInference',
        taskUUID,
        positivePrompt: fullPrompt,
        negativePrompt: 'blurry, low quality, text, watermark, signature, ugly, deformed, amateur, pixelated, dark background, black background, complex background, noisy background, multiple objects',
        model: MODEL,
        width: 256,
        height: 256,
        outputFormat: 'PNG',
        numberResults: 1,
    }];

    const res = await fetch(API_URL, {
        method: 'POST',
        headers: { 'Authorization': `Bearer ${API_KEY}`, 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
    });

    if (!res.ok) throw new Error(`Generate error ${res.status}: ${await res.text()}`);
    const data = await res.json();
    if (data.data?.[0]?.imageURL) return data.data[0].imageURL;
    throw new Error(`Unexpected response: ${JSON.stringify(data)}`);
}

async function removeBackground(imageURL) {
    const taskUUID = randomUUID();
    const body = [{
        taskType: 'imageBackgroundRemoval',
        taskUUID,
        inputImage: imageURL,
        outputFormat: 'PNG',
    }];

    const res = await fetch(API_URL, {
        method: 'POST',
        headers: { 'Authorization': `Bearer ${API_KEY}`, 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
    });

    if (!res.ok) throw new Error(`BG removal error ${res.status}: ${await res.text()}`);
    const data = await res.json();
    if (data.data?.[0]?.imageURL) return data.data[0].imageURL;
    throw new Error(`BG removal unexpected: ${JSON.stringify(data)}`);
}

async function downloadImage(url, outputPath) {
    const res = await fetch(url);
    if (!res.ok) throw new Error(`Download failed: ${res.status}`);
    const buffer = Buffer.from(await res.arrayBuffer());
    await writeFile(outputPath, buffer);
    console.log(`  ✓ Saved: ${path.basename(outputPath)}`);
}

// Main
if (!existsSync(OUT_DIR)) await mkdir(OUT_DIR, { recursive: true });

console.log('Regenerating resource icons with transparent backgrounds...\n');
const start = Date.now();
let success = 0;

for (const icon of ICONS) {
    console.log(`  ${icon.id}:`);
    try {
        console.log('    generating on white bg...');
        const genUrl = await generateImage(icon.prompt);
        console.log('    removing background...');
        const cleanUrl = await removeBackground(genUrl);
        await downloadImage(cleanUrl, path.join(OUT_DIR, `${icon.id}.png`));
        success++;
        await new Promise(r => setTimeout(r, 400));
    } catch (err) {
        console.error(`    ✗ Failed: ${err.message}`);
    }
}

console.log(`\n✓ Done: ${success}/${ICONS.length} icons in ${((Date.now() - start) / 1000).toFixed(1)}s`);
