const fs = require('fs');
const path = require('path');

const sourceDir = path.join(__dirname, '../AI');
const targetBaseDir = path.join(__dirname, '../dist/character-images');

const imageExtensions = ['.jpg', '.jpeg', '.png', '.gif'];

console.log(`Source directory: ${sourceDir}`);
console.log(`Target directory: ${targetBaseDir}`);

// Create the base target directory if it doesn't exist
if (!fs.existsSync(targetBaseDir)) {
    console.log(`Creating target base directory: ${targetBaseDir}`);
    fs.mkdirSync(targetBaseDir, { recursive: true });
} else {
    console.log(`Target base directory already exists: ${targetBaseDir}`);
}

try {
    const characterFolders = fs.readdirSync(sourceDir, { withFileTypes: true })
        .filter(dirent => dirent.isDirectory())
        .map(dirent => dirent.name);

    if (characterFolders.length === 0) {
        console.log(`No character folders found in ${sourceDir}`);
    }

    for (const characterFolder of characterFolders) {
        const sourceCharacterDir = path.join(sourceDir, characterFolder);
        const targetCharacterDir = path.join(targetBaseDir, characterFolder);

        console.log(`Processing character: ${characterFolder}`);
        console.log(`  Source: ${sourceCharacterDir}`);
        console.log(`  Target: ${targetCharacterDir}`);

        // Create character-specific target directory
        if (!fs.existsSync(targetCharacterDir)) {
            console.log(`  Creating character target directory: ${targetCharacterDir}`);
            fs.mkdirSync(targetCharacterDir, { recursive: true });
        } else {
            console.log(`  Character target directory already exists: ${targetCharacterDir}`);
        }

        const files = fs.readdirSync(sourceCharacterDir);
        let imagesCopied = 0;

        for (const file of files) {
            const sourceFilePath = path.join(sourceCharacterDir, file);
            const targetFilePath = path.join(targetCharacterDir, file);
            const ext = path.extname(file).toLowerCase();

            if (imageExtensions.includes(ext)) {
                try {
                    fs.copyFileSync(sourceFilePath, targetFilePath);
                    console.log(`    Copied ${file} to ${targetCharacterDir}`);
                    imagesCopied++;
                } catch (copyError) {
                    console.error(`    Error copying ${file}: ${copyError.message}`);
                }
            }
        }
        if (imagesCopied === 0) {
            console.log(`  No images found or copied for character ${characterFolder}.`);
        } else {
            console.log(`  Copied ${imagesCopied} image(s) for character ${characterFolder}.`);
        }
    }
    console.log('Character asset copying process completed.');

} catch (error) {
    console.error(`Error processing character assets: ${error.message}`);
    if (error.code === 'ENOENT' && error.path === sourceDir) {
        console.error(`Source directory ${sourceDir} not found. Please ensure it exists and contains character folders.`);
    }
    process.exit(1); // Exit with error
}
