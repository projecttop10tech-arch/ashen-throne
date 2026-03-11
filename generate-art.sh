#!/bin/bash
# Runware AI Art Generator for Ashen Throne
# Generates all game art assets using AI image generation
set -e

API_KEY="VBp08pohtQitvDN8nmdfK0XlIxkV0POS"
API_URL="https://api.runware.ai/v1"
ART_DIR="/Users/tomterhune/ashen-throne/game/Assets/Art"

# Create output directories
mkdir -p "$ART_DIR/AI/Splash"
mkdir -p "$ART_DIR/AI/Backgrounds"
mkdir -p "$ART_DIR/AI/Heroes"
mkdir -p "$ART_DIR/AI/Cards"
mkdir -p "$ART_DIR/AI/Buildings"
mkdir -p "$ART_DIR/AI/Icons"

generate_image() {
    local prompt="$1"
    local width="$2"
    local height="$3"
    local output_path="$4"
    local uuid=$(uuidgen | tr '[:upper:]' '[:lower:]')

    echo "  Generating: $(basename "$output_path")..."

    local response=$(curl -s --location "$API_URL" \
        --header 'Content-Type: application/json' \
        --data-raw "[
            {\"taskType\": \"authentication\", \"apiKey\": \"$API_KEY\"},
            {
                \"taskType\": \"imageInference\",
                \"taskUUID\": \"$uuid\",
                \"positivePrompt\": \"$prompt\",
                \"negativePrompt\": \"text, watermark, signature, blurry, low quality, ugly, deformed, amateur, stock photo, border, frame\",
                \"width\": $width,
                \"height\": $height,
                \"model\": \"runware:100@1\",
                \"numberResults\": 1,
                \"outputFormat\": \"PNG\"
            }
        ]")

    # Extract image URL from response
    local image_url=$(echo "$response" | jq -r '.data[0].imageURL // empty' 2>/dev/null | head -1)

    if [ -z "$image_url" ] || [ "$image_url" = "null" ]; then
        echo "    ERROR: No image URL in response"
        echo "    Response: $(echo "$response" | head -c 500)"
        return 1
    fi

    # Download image
    curl -s -o "$output_path" "$image_url"
    local filesize=$(stat -f%z "$output_path" 2>/dev/null || stat --printf="%s" "$output_path" 2>/dev/null)
    echo "    OK ($filesize bytes)"
}

echo "========================================"
echo "  ASHEN THRONE — AI Art Generation"
echo "  Using Runware API"
echo "========================================"
echo ""

# ============================================================
# 1. SPLASH SCREEN (1080x1920)
# ============================================================
echo "[1/7] Splash Screen"
generate_image \
    "dark fantasy castle on a mountain peak, glowing ice blue magic beam shooting into stormy sky, epic cinematic lighting, snow-covered peaks, dragons silhouettes in clouds, dark moody atmosphere, concept art, matte painting style, 4k, highly detailed, game splash screen art" \
    1024 1792 \
    "$ART_DIR/AI/Splash/splash_main.png"

# ============================================================
# 2. SCENE BACKGROUNDS (1080x1920 portrait)
# ============================================================
echo ""
echo "[2/7] Scene Backgrounds"

generate_image \
    "dark fantasy throne room interior, ornate gothic architecture, purple magical energy, crystal chandeliers, velvet and gold decorations, moody lighting, game UI background, highly detailed digital painting" \
    1024 1792 \
    "$ART_DIR/AI/Backgrounds/lobby_bg.png"

generate_image \
    "dark fantasy battlefield, volcanic terrain with lava cracks, tactical grid overlaid on stone arena, dark stormy sky, embers floating, epic battle atmosphere, top-down perspective, game combat background, highly detailed" \
    1024 1792 \
    "$ART_DIR/AI/Backgrounds/combat_bg.png"

generate_image \
    "dark fantasy medieval city overhead view, gothic buildings and towers, torchlit streets, dark stone architecture, mystical green fog, aerial perspective, city builder game background, highly detailed digital art" \
    1024 1792 \
    "$ART_DIR/AI/Backgrounds/empire_bg.png"

generate_image \
    "dark fantasy world map, parchment style with mountains forests deserts, dragon territory markings, glowing magical borders between regions, medieval cartography style, strategy game world map, highly detailed" \
    1024 1792 \
    "$ART_DIR/AI/Backgrounds/worldmap_bg.png"

generate_image \
    "dark fantasy guild hall interior, long wooden tables, banners and shields on walls, roaring fireplace, candlelight, medieval tavern atmosphere, warm but dark mood, game alliance screen background" \
    1024 1792 \
    "$ART_DIR/AI/Backgrounds/alliance_bg.png"

# ============================================================
# 3. HERO PORTRAITS (512x512)
# ============================================================
echo ""
echo "[3/7] Hero Portraits"

declare -a HERO_PROMPTS=(
    "dark fantasy male warrior portrait, fiery red hair, burning ember eyes, ornate dark armor with gold trim, scarred face, determined expression, dramatic lighting, highly detailed digital painting style|kael_ashwalker"
    "dark fantasy female ranger portrait, silver-white hair with thorny vine crown, green glowing eyes, leather armor with nature motifs, ethereal beauty, moonlit, highly detailed digital painting|lyra_thornveil"
    "dark fantasy male paladin portrait, iron grey beard, blue glowing eyes, massive plate armor with runes, weathered noble face, ice theme, highly detailed digital painting|thane_ironhold"
    "dark fantasy female sorceress portrait, wild electric blue hair, crackling lightning around hands, dark robes with storm patterns, fierce expression, dramatic purple lightning background, highly detailed|nyx_stormcaller"
    "dark fantasy male assassin portrait, dark hooded figure, glowing red eyes in shadow, dual daggers, scarred face half-hidden, smoke and shadow effects, menacing, highly detailed digital painting|vex_shadowstrike"
    "dark fantasy female ice mage portrait, pale blue skin, crystalline ice crown, frost breath, elegant ice-blue robes, serene expression, frozen particles in air, highly detailed digital painting|mira_frostbane"
    "dark fantasy male berserker portrait, massive orc-like warrior, bone jewelry and war paint, glowing green eyes, savage expression, dark tribal armor, skull decorations, highly detailed digital painting|grim_bonecrusher"
    "dark fantasy male earth knight portrait, stone-like skin texture, amber eyes, massive stone shield, mossy ancient armor, steadfast expression, mountain background, highly detailed digital painting|rowan_stoneward"
    "dark fantasy female void witch portrait, dark purple skin, galaxy eyes with stars, flowing dark robes with void tendrils, mysterious ethereal glow, cosmic horror beauty, highly detailed digital painting|zara_voidweaver"
    "dark fantasy female holy knight portrait, golden armored angel, radiant halo, flowing blonde hair, holy sword, divine light, white and gold paladin armor, fierce righteous expression, highly detailed|sera_dawnblade"
)

for entry in "${HERO_PROMPTS[@]}"; do
    IFS='|' read -r prompt name <<< "$entry"
    generate_image "$prompt" 512 512 "$ART_DIR/AI/Heroes/${name}_portrait.png"
done

# ============================================================
# 4. CARD FRAMES (512x768)
# ============================================================
echo ""
echo "[4/7] Card Art"

declare -a CARD_PROMPTS=(
    "fire magic ability card art, explosive fireball erupting, dark fantasy style, ornate golden frame border, ember particles, dramatic red and orange, game card illustration, no text|card_fire"
    "ice magic ability card art, freezing ice crystal explosion, dark fantasy style, ornate silver frame border, frost particles, dramatic blue and white, game card illustration, no text|card_ice"
    "shadow magic ability card art, dark void tendrils reaching out, dark fantasy style, ornate purple frame border, shadow particles, dramatic purple and black, game card illustration, no text|card_shadow"
    "holy healing ability card art, radiant golden light healing aura, dark fantasy style, ornate white gold frame, divine particles, warm gold and white, game card illustration, no text|card_holy"
    "nature magic ability card art, thorny vines bursting from earth, dark fantasy style, ornate green frame border, leaf particles, rich green and brown, game card illustration, no text|card_nature"
    "lightning ability card art, massive lightning bolt strike, dark fantasy style, ornate electric blue frame, spark particles, dramatic blue and purple, game card illustration, no text|card_lightning"
)

for entry in "${CARD_PROMPTS[@]}"; do
    IFS='|' read -r prompt name <<< "$entry"
    generate_image "$prompt" 512 768 "$ART_DIR/AI/Cards/${name}.png"
done

# ============================================================
# 5. BUILDINGS (512x512)
# ============================================================
echo ""
echo "[5/7] Buildings"

declare -a BUILDING_PROMPTS=(
    "dark fantasy stronghold castle, isometric view, gothic tower with dark stone and gold accents, magical purple glow from windows, game building icon, transparent background style, highly detailed|stronghold"
    "dark fantasy barracks building, isometric view, military training ground, wooden and stone structure, weapon racks, torches, game building icon, highly detailed|barracks"
    "dark fantasy forge building, isometric view, blacksmith workshop with glowing furnace, anvil, smoke chimney, molten metal glow, game building icon, highly detailed|forge"
    "dark fantasy marketplace building, isometric view, merchant stalls with colorful fabrics, lanterns, barrels and crates, bustling trade post, game building icon, highly detailed|marketplace"
    "dark fantasy arcane tower, isometric view, mystical glowing purple crystal on top, runic inscriptions, magical energy swirls, wizard tower, game building icon, highly detailed|arcane_tower"
    "dark fantasy farm, isometric view, medieval wheat fields, windmill, golden crop glow, rustic fence, game building icon, highly detailed|farm"
)

for entry in "${BUILDING_PROMPTS[@]}"; do
    IFS='|' read -r prompt name <<< "$entry"
    generate_image "$prompt" 512 512 "$ART_DIR/AI/Buildings/${name}.png"
done

# ============================================================
# 6. RESOURCE ICONS (256x256)
# ============================================================
echo ""
echo "[6/7] Resource Icons"

declare -a ICON_PROMPTS=(
    "golden dragon coin icon, dark fantasy style, shiny metallic gold, embossed dragon symbol, game UI icon, transparent background, highly detailed|icon_gold"
    "purple magical crystal gem icon, dark fantasy style, glowing purple faceted gem, magical sparkles, game UI icon, transparent background, highly detailed|icon_gems"
    "grey stone block icon, dark fantasy style, rough hewn stone with cracks, mineral deposits, game resource icon, transparent background, highly detailed|icon_stone"
    "dark iron ingot icon, dark fantasy style, polished dark metal bar with blue sheen, game resource icon, transparent background, highly detailed|icon_iron"
    "golden wheat sheaf icon, dark fantasy style, bundle of golden wheat stalks, warm glow, game resource icon, transparent background, highly detailed|icon_grain"
    "arcane essence vial icon, dark fantasy style, glowing purple potion bottle with swirling energy, magical particles, game resource icon, transparent background, highly detailed|icon_arcane"
    "energy orb icon, dark fantasy style, glowing teal magical sphere, crackling energy, game UI icon, transparent background, highly detailed|icon_energy"
    "golden star rating icon, dark fantasy style, ornate 5-pointed star, glowing gold, game UI icon, transparent background|icon_star"
)

for entry in "${ICON_PROMPTS[@]}"; do
    IFS='|' read -r prompt name <<< "$entry"
    generate_image "$prompt" 256 256 "$ART_DIR/AI/Icons/${name}.png"
done

# ============================================================
# 7. NAV BAR ICONS (128x128)
# ============================================================
echo ""
echo "[7/7] Navigation Icons"

declare -a NAV_PROMPTS=(
    "dark fantasy castle tower icon, gold metallic style, game navigation icon, clean silhouette, transparent background|nav_empire"
    "dark fantasy crossed swords icon, gold metallic style, game navigation battle icon, clean silhouette, transparent background|nav_battle"
    "dark fantasy warrior shield icon with sword, gold metallic style, game navigation hero icon, clean silhouette, transparent background|nav_heroes"
    "dark fantasy scroll and quill icon, gold metallic style, game navigation quest icon, clean silhouette, transparent background|nav_quest"
    "dark fantasy banner and alliance crest icon, gold metallic style, game navigation social icon, clean silhouette, transparent background|nav_alliance"
    "dark fantasy treasure chest icon, gold metallic style, game navigation shop icon, clean silhouette, transparent background|nav_shop"
    "dark fantasy envelope with wax seal icon, gold metallic style, game navigation mail icon, clean silhouette, transparent background|nav_mail"
)

for entry in "${NAV_PROMPTS[@]}"; do
    IFS='|' read -r prompt name <<< "$entry"
    generate_image "$prompt" 256 256 "$ART_DIR/AI/Icons/${name}.png"
done

echo ""
echo "========================================"
echo "  GENERATION COMPLETE"
echo "========================================"
echo "  Assets saved to: $ART_DIR/AI/"
ls -la "$ART_DIR/AI/"/*/ 2>/dev/null | grep -c ".png" && echo " PNG files generated" || echo "  Check output above for errors"
echo "========================================"
