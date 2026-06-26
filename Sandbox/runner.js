const fs = require('fs');
const path = require('path');

// Read all input from standard input
const input = fs.readFileSync(0, 'utf-8').trim().split(/\r?\n/);
let currentLine = 0;

// Attach functions to the Node.js global object
global.readline = () => input[currentLine++];
global.print = (...args) => console.log(...args);

// Execute your solution file
const fileToRun = process.argv[2];
if (fileToRun) {
    // Resolve the absolute path based on where you ran the terminal command
    const absolutePath = path.resolve(process.cwd(), fileToRun);
    require(absolutePath);
} else {
    console.error("Please provide a file to run. Example: node runner.js solution.js");
}