# SwimStats Configuration Guide

## Swimmer Configuration

The SwimStats application manages its swimmer list through a JSON configuration file, which allows you to easily add, remove, or modify swimmers without any code changes.

### Location

The `swimmers.json` configuration file is located at:
```
%LOCALAPPDATA%\SwimStats\swimmers.json
```

On Windows, this typically expands to:
```
C:\Users\[YourUsername]\AppData\Local\SwimStats\swimmers.json
```

### How to Edit Swimmers

1. **Open the Configuration File**
   - Open File Explorer
   - Press `Ctrl+L` to access the address bar
   - Paste: `%LOCALAPPDATA%\SwimStats`
   - Find and open `swimmers.json` with a text editor (Notepad, VS Code, etc.)

2. **File Format**
   The swimmers.json file contains a JSON array of swimmer objects. Each swimmer has:
   - `id`: A unique integer identifier for the swimmer
   - `name`: The full name of the swimmer

   Example:
   ```json
   [
     { "id": 1, "name": "John Smith" },
     { "id": 2, "name": "Jane Doe" },
     { "id": 3, "name": "Alice Johnson" }
   ]
   ```

3. **Adding a Swimmer**
   - Open `swimmers.json` in a text editor
   - Find an unused ID number (typically use the next sequential number)
   - Add a new entry to the array:
   ```json
   { "id": 31, "name": "New Swimmer Name" }
   ```
   - Make sure to add a comma after the previous entry if it's not the last one
   - Save the file

4. **Removing a Swimmer**
   - Open `swimmers.json`
   - Delete the entire line containing the swimmer you want to remove
   - Make sure the JSON syntax remains valid (check commas)
   - Save the file

5. **Modifying a Swimmer**
   - Open `swimmers.json`
   - Find the swimmer you want to edit
   - Change the `name` value (do not change the `id`)
   - Save the file

### Important Notes

- **ID Uniqueness**: Each `id` must be unique. Do not use duplicate IDs.
- **JSON Syntax**: Be careful with commas and quotes. Incorrect JSON syntax will prevent the file from loading.
- **Restart Required**: Changes to `swimmers.json` will take effect the next time you start the SwimStats application.
- **Valid JSON**: If you're unsure about your edits, use a JSON validator tool online to check your file before saving.

### Example JSON

```json
[
  { "id": 1, "name": "Ingemar Voskamp" },
  { "id": 2, "name": "Cindy Franken-Hendriks" },
  { "id": 3, "name": "Tom Brouwers" },
  { "id": 4, "name": "Annemarie Jakobs" },
  { "id": 5, "name": "Esther Sprick" }
]
```

### Troubleshooting

**Issue**: Application crashes on startup  
**Solution**: Check that your `swimmers.json` file has valid JSON syntax. Use an online JSON validator.

**Issue**: New swimmers don't appear in the application  
**Solution**: 
1. Make sure you saved the file
2. Restart the SwimStats application
3. Check that the JSON syntax is correct

**Issue**: I accidentally corrupted the swimmers.json file  
**Solution**: 
1. Delete the corrupted `swimmers.json` file
2. Restart SwimStats - it will automatically restore the default swimmers.json from the installation
3. Make your changes again more carefully

---

For more information about SwimStats, see the main README.md file.
