#!/usr/bin/env node
/**
 * Runware AI Audio Generator for Ashen Throne
 * Generates hero voice lines via TTS and additional SFX stubs.
 *
 * Usage: node tools/generate-audio.mjs [category]
 * Categories: voices, music, all
 */

import { randomUUID } from 'crypto';
import { writeFile, mkdir } from 'fs/promises';
import { existsSync } from 'fs';
import path from 'path';

const API_URL = 'https://api.runware.ai/v1';
const API_KEY = process.env.RUNWARE_API_KEY || 'mrkrkANAo0C2Cvi1w8UZqn95JK64ngJc';
const AUDIO_DIR = path.join(import.meta.dirname, '..', 'game', 'Assets', 'Audio');
const TTS_MODEL = 'minimax:speech@2.8';

const HEROES_VOICE_LINES = [
    {
        id: 'kael_ashwalker',
        voice: 'English_magnetic_voiced_man',
        lines: {
            select: 'The ashes remember.',
            combat_start: 'Burn them all!',
            victory: 'From the ashes, we rise.',
            defeated: 'The fire... fades...',
        }
    },
    {
        id: 'sera_dawnblade',
        voice: 'English_sweet_lady',
        lines: {
            select: 'Light guides my blade.',
            combat_start: 'For the dawn!',
            victory: 'Justice prevails.',
            defeated: 'The light... grows dim...',
        }
    },
    {
        id: 'vex_shadowstrike',
        voice: 'English_magnetic_voiced_man',
        lines: {
            select: 'Shadows hide many secrets.',
            combat_start: 'From the darkness, I strike!',
            victory: 'They never saw me coming.',
            defeated: 'Even shadows... can bleed...',
        }
    },
    {
        id: 'lyra_thornveil',
        voice: 'English_sweet_lady',
        lines: {
            select: 'Nature calls to me.',
            combat_start: 'Feel the thorns bite!',
            victory: 'The forest stands eternal.',
            defeated: 'Return me... to the earth...',
        }
    },
    {
        id: 'thane_ironhold',
        voice: 'English_magnetic_voiced_man',
        lines: {
            select: 'Iron and honor!',
            combat_start: 'By my hammer, you fall!',
            victory: 'Another victory forged in iron!',
            defeated: 'Even iron... bends...',
        }
    },
    {
        id: 'nyx_stormcaller',
        voice: 'English_sweet_lady',
        lines: {
            select: 'The storm obeys my will.',
            combat_start: 'Thunder and lightning!',
            victory: 'The tempest claims another.',
            defeated: 'The storm... passes...',
        }
    },
    {
        id: 'grim_bonecrusher',
        voice: 'English_magnetic_voiced_man',
        lines: {
            select: 'Death is only the beginning.',
            combat_start: 'Rise, my minions!',
            victory: 'Your bones will serve me well.',
            defeated: 'I shall... return...',
        }
    },
    {
        id: 'mira_frostbane',
        voice: 'English_sweet_lady',
        lines: {
            select: 'Cold as the grave.',
            combat_start: 'Freeze!',
            victory: 'Winter claims all in the end.',
            defeated: 'The ice... melts...',
        }
    },
    {
        id: 'rowan_stoneward',
        voice: 'English_magnetic_voiced_man',
        lines: {
            select: 'Solid as bedrock.',
            combat_start: 'You shall not pass!',
            victory: 'Unbreakable.',
            defeated: 'Even mountains... crumble...',
        }
    },
    {
        id: 'zara_voidweaver',
        voice: 'English_sweet_lady',
        lines: {
            select: 'The void whispers to me.',
            combat_start: 'Reality bends to my will!',
            victory: 'The void consumes all.',
            defeated: 'The void... reclaims me...',
        }
    },
];

async function generateTTS(text, voice) {
    const taskUUID = randomUUID();
    const body = [{
        taskType: 'audioInference',
        taskUUID,
        model: TTS_MODEL,
        positivePrompt: text,
        voice: voice,
    }];

    const res = await fetch(API_URL, {
        method: 'POST',
        headers: {
            'Authorization': `Bearer ${API_KEY}`,
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(body),
    });

    if (!res.ok) {
        const errText = await res.text();
        throw new Error(`TTS API error ${res.status}: ${errText}`);
    }

    const data = await res.json();
    if (data.data && data.data.length > 0 && data.data[0].audioURL) {
        return data.data[0].audioURL;
    }
    throw new Error(`Unexpected TTS response: ${JSON.stringify(data)}`);
}

async function downloadAudio(url, outputPath) {
    const res = await fetch(url);
    if (!res.ok) throw new Error(`Download failed: ${res.status}`);
    const buffer = Buffer.from(await res.arrayBuffer());
    await writeFile(outputPath, buffer);
    console.log(`  ✓ Saved: ${path.basename(outputPath)}`);
}

async function ensureDir(dir) {
    if (!existsSync(dir)) await mkdir(dir, { recursive: true });
}

function sleep(ms) {
    return new Promise(r => setTimeout(r, ms));
}

async function generateVoiceLines() {
    console.log('\n═══ Generating Hero Voice Lines ═══');
    const voiceDir = path.join(AUDIO_DIR, 'Voice');
    await ensureDir(voiceDir);

    for (const hero of HEROES_VOICE_LINES) {
        console.log(`\n  Hero: ${hero.id}`);
        const heroDir = path.join(voiceDir, hero.id);
        await ensureDir(heroDir);

        for (const [lineType, text] of Object.entries(hero.lines)) {
            try {
                const url = await generateTTS(text, hero.voice);
                await downloadAudio(url, path.join(heroDir, `${lineType}.wav`));
                await sleep(300);
            } catch (err) {
                console.error(`  ✗ Failed: ${hero.id}/${lineType} — ${err.message}`);
            }
        }
    }
}

// -------------------------------------------------------------------
// Main
// -------------------------------------------------------------------

const category = process.argv[2] || 'all';
console.log(`\n🎵 Ashen Throne Audio Generator — Category: ${category}`);

const start = Date.now();

try {
    switch (category) {
        case 'voices': await generateVoiceLines(); break;
        case 'all': await generateVoiceLines(); break;
        default:
            console.error(`Unknown category: ${category}`);
            console.log('Valid: voices, all');
            process.exit(1);
    }
} catch (err) {
    console.error(`\n✗ Fatal error: ${err.message}`);
}

const elapsed = ((Date.now() - start) / 1000).toFixed(1);
console.log(`\n✓ Done in ${elapsed}s`);
