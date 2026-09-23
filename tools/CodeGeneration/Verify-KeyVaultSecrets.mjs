import { readFileSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const extension = resolve(root, 'src/Extensions/KeyVault');
const manifestName = process.argv[3] ?? 'secrets-generation.json';
if (!['secrets-generation.json', 'certificates-generation.json'].includes(manifestName)) throw new Error('Unknown generation manifest.');
const manifest = JSON.parse(readFileSync(resolve(extension, manifestName), 'utf8'));
const raw = process.argv[2];
if (!raw || process.argv.length > 4) {
    console.error('Usage: node tools/CodeGeneration/Verify-KeyVaultSecrets.mjs <raw-output> [secrets-generation.json|certificates-generation.json]');
    process.exit(2);
}

const normalize = content => content.replaceAll('\r\n', '\n').trimEnd() + '\n';
const files = [
    ...manifest.models.map(name => ({ file: `Models/${name}.cs` })),
    ...manifest.processors.map(processor => ({ file: `Processors/${processor.name}.cs`, ...processor }))
];
const failures = [];
for (const entry of files) {
    let expected = normalize(readFileSync(resolve(raw, entry.file), 'utf8'));
    for (const rule of manifest.literalReplacements) expected = expected.replaceAll(rule.from, rule.to);
    if (entry.requestUri) {
        const pattern = /^    protected override string CreateRequestUri\(\) => [^\n]+;$/gm;
        if ([...expected.matchAll(pattern)].length !== 1) throw new Error(`Unexpected raw URI declaration: ${entry.file}`);
        expected = expected.replace(pattern, () => entry.requestUri);
    }
    const actual = normalize(readFileSync(resolve(extension, entry.file), 'utf8'));
    if (actual !== expected) failures.push(entry.file);
}
if (failures.length) {
    console.error(`Undocumented generated-code differences:\n${failures.join('\n')}`);
    process.exit(1);
}
console.log(`Verified ${files.length} files against raw generated output and the declared polishing rules.`);