# RecoverSaveGen3
 Try and recover a Gen3 save file via console app!

Requires .NET 8 runtime installed, can be run on any operating system (not Windows specific)!

## Usage:
* Drag and drop your save file onto the `.exe` and the command line window will indicate the conversion result.
* If successful, the recovered file will be saved to the same path as the file you wanted to convert. 
- The file extension `.fixed` is added to differentiate from the input file. Removing it is fine.

## How it works:
* Based on the save file structure of generation 3 save files, the program will try and grab the latest save block for each block.
* If not enough blocks are available, the program will be unable to recover anything.
* If box data is partially missing (usually what happens), then the program will provide fake blocks, effectively clearing that portion of the box.
* Once recovered, the program will update all checksums/identifiers to ensure it will be recognized as "valid".
