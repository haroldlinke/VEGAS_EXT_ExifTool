# VEGAS-EXT_ExifTool
This project provides a custom command extension for VEGAS Pro that shows all EXIF-Data of clips on the VEGAS timeline or in the mdeia pool.

# Installation:
- Download the zip archive Vegas-ExifTool.zip from the last release in this github repository.

- Unzip the content into the folder C:\ProgramData\VEGAS Pro\Application Extensions. (If this folder does not exist on your system, please create it).
- After restart of VEGAS Pro you can activate the extension by selecting the option
"view->extensions->ExifTool Metadata" a dockable window will open. You can dock this window in the VEGAS Pro UI where you want or keep it floating.

# Usage
The tool shows the Exif data of the first selected clip on the timeline or in Project Media.

The extension provides a ComboBox with the options: "Auto", "Timeline", "Project Media".
- "Auto": The slection is automatic. First timeline the project Media
- "Timeline": Selecting a clip on the timeline shows immediatly the EXIF-data of this clip
- "Project Media": Selecting a clip in the "Project Media" shows immediatly the EXIF-data of this clip
- "None": No Exif data is determined and shown.

  The tool is based on the EXIFTool by ExifTool by Phil Harvey (https://exiftool.org/)


