using System;
using System.Diagnostics;
using System.Net;
using System.Threading;
using static balatro_mobile_maker.Constants;
using static balatro_mobile_maker.Tools;
using static balatro_mobile_maker.Program;

namespace balatro_mobile_maker;

/// <summary>
/// Command line UI for Balatro APK Maker.
/// </summary>
// NOTE: Much should be refactored out of UI logic land, and, into a Controller which we can query state from.
internal class View
{

    private bool _androidBuild;
    private bool _iosBuild;
    private bool _webBuild;

    private static bool _cleaup;

    static bool gameProvided;

    /// <summary>
    /// Start CLI operation.
    /// </summary>
    public void Begin()
    {
        Log("====Balatro Platforms Port====\n");

        //Initial prompts
        _cleaup = AskQuestion("Would you like to automatically clean up once complete?");
        _verboseMode = AskQuestion("Would you like to enable extra logging information?");

        //If balatro.apk or balatro.ipa already exists, ask before beginning build process again
        if (!(fileExists("balatro.apk") || fileExists("balatro.ipa") || directoryExists("web-build")) || AskQuestion("A previous build was found... Would you like to build again?"))
        {
            _androidBuild = AskQuestion("Would you like to build for Android?");
            _iosBuild = AskQuestion("Would you like to build for iOS (experimental)?");
            _webBuild = AskQuestion("Would you like to build for Web?");


            if (_androidBuild || _iosBuild || _webBuild)
            {
                #region Download tools
                if (_androidBuild)
                {
                    #region Android tools
                    //Downloading tools. Handled in threads to allow simultaneous downloads
                    Thread[] downloadThreads =
                    [
                        new Thread(() => { TryDownloadFile("OpenJDK", Platform.getOpenJDKDownloadLink(), "openjdk"); }),

                        new Thread(() => { TryDownloadFile("APKTool", ApktoolLink, "apktool.jar"); }),
                        new Thread(() => { TryDownloadFile("uber-apk-signer", UberapktoolLink, "uber-apk-signer.jar"); }),
                        new Thread(() => { TryDownloadFile("Balatro-APK-Patch", BalatroApkPatchLink, "Balatro-APK-Patch.zip"); }),
                        new Thread(() => { TryDownloadFile("Love2D APK", Love2dApkLink, "love-11.5-android-embed.apk"); })
                    ];

                    //Start all the downloads
                    foreach (var t in downloadThreads) t.Start();

                    //Wait for all the downloads to complete
                    foreach (var t in downloadThreads) t.Join();

                    #endregion
                }

                if (_iosBuild)
                {
                    #region iOS Tools
                    //Downloading tools. Handled in threads to allow simultaneous downloads
                    Thread[] downloadThreads =
                    [
                        new Thread(() => { TryDownloadFile("iOS Base", IosBaseLink, "balatro-base.ipa"); })
                    ];

                    //Start all the downloads
                    foreach (var t in downloadThreads) t.Start();

                    //Wait for all the downloads to complete
                    foreach (var t in downloadThreads) t.Join();
                    #endregion
                }
                #endregion

                #region Prepare workspace
                #region Find and extract Balatro.exe

                bool webGameLoveProvided = false;
                if (_webBuild && !(_androidBuild || _iosBuild))
                {
                    // For web build, we need Balatro.exe
                    Log("For Web build, you need to provide Balatro.exe.");
                }

                gameProvided = Platform.gameExists();

                if (gameProvided)
                    Log("Game found!");
                else
                {
                    //Game not provided
                    //Try to locate automatically
                    if (Platform.tryLocateGame())
                        Log("Game copied!");
                    else
                    {
                        //Game not provided, and could not be located
                        Log("Could not find Balatro.exe! Please place it in this folder, then try again!");
                        Exit();
                    }
                }

                Log("Extracting Balatro.exe...");
                if (directoryExists("Balatro"))
                {
                    //Delete the Balatro folder if it already exists
                    Log("Balatro directory already exists! Deleting Balatro directory...");
                    tryDelete("Balatro");
                }

                //Extract Balatro.exe
                extractZip("Balatro.exe", "Balatro");

                //Check for failure
                if (!directoryExists("Balatro"))
                {
                    Log("Failed to extract Balatro.exe!");
                    Exit();
                }
                #endregion

                if (_androidBuild)
                {
                    #region Extract APK
                    Log("Unpacking Love2D APK with APK Tool...");
                    if (directoryExists("balatro-apk"))
                    {
                        //Delete the balatro-apk folder if it already exists
                        Log("balatro-apk directory already exists! Deleting balatro-apk directory...");
                        tryDelete("balatro-apk");
                    }

                    //Unpack Love2D APK
                    useTool(ProcessTools.Java, "-jar -Xmx1G -Duser.language=en -Dfile.encoding=UTF8 -Djdk.util.zip.disableZip64ExtraFieldValidation=true -Djdk.nio.zipfs.allowDotZipEntry=true \"apktool.jar\" d -s -o balatro-apk love-11.5-android-embed.apk");

                    //Check for failure
                    if (!directoryExists("balatro-apk"))
                    {
                        Log("Failed to unpack Love2D APK with APK Tool!");
                        Exit();
                    }
                    #endregion

                    #region APK patch
                    Log("Extracting patch zip...");
                    if (directoryExists("Balatro-APK-Patch"))
                    {
                        Log("Balatro-APK-Patch directory already exists! Deleting Balatro-APK-Patch directory...");
                        tryDelete("Balatro-APK-Patch");
                    }

                    //Extract Balatro-APK-Patch
                    extractZip("Balatro-APK-Patch.zip", "Balatro-APK-Patch");

                    if (!directoryExists("Balatro-APK-Patch"))
                    {
                        Log("Failed to extract Balatro-APK-Patch");
                        Exit();
                    }

                    //Base APK patch
                    Log("Patching APK folder...");
                    //This isn't pretty, but I'm planning to change how icons are done at some point. So this is fine for now.
                    fileCopy("Balatro-APK-Patch/AndroidManifest.xml", "balatro-apk/AndroidManifest.xml");
                    fileCopy("Balatro-APK-Patch/res/drawable-hdpi/love.png", "balatro-apk/res/drawable-hdpi/love.png");
                    fileCopy("Balatro-APK-Patch/res/drawable-mdpi/love.png", "balatro-apk/res/drawable-mdpi/love.png");
                    fileCopy("Balatro-APK-Patch/res/drawable-xhdpi/love.png", "balatro-apk/res/drawable-xhdpi/love.png");
                    fileCopy("Balatro-APK-Patch/res/drawable-xxhdpi/love.png", "balatro-apk/res/drawable-xxhdpi/love.png");
                    fileCopy("Balatro-APK-Patch/res/drawable-xxxhdpi/love.png", "balatro-apk/res/drawable-xxxhdpi/love.png");
                    #endregion
                }

                if (_iosBuild)
                {
                    #region Prepare IPA
                    Log("Preparing iOS Base...");
                    fileMove("balatro-base.ipa", "balatro-base.zip");
                    #endregion
                }

                #endregion

                #region Patch
                Log("Patching...");
                Patching.Begin();
                #endregion

                #region Building

                #region Balatro.exe -> game.love
                Log("Packing Balatro folder...");
                
                if (_webBuild)
                {
                    // Copy bitops.lua to Balatro directory for web build
                    Log("Checking for bitops.lua for web build compatibility...");
                    if (fileExists("vendor/bitops.lua") && !fileExists("Balatro/bitops.lua"))
                    {
                        Log("Copying vendor/bitops.lua to Balatro directory for web compatibility...");
                        fileCopy("vendor/bitops.lua", "Balatro/bitops.lua");
                        Patching.ApplyPatch("main.lua", @"require ""bit""", @"local bit = require ""bitops""");
                    }
                    else if (!fileExists("vendor/bitops.lua") && !fileExists("Balatro/bitops.lua"))
                    {
                        Log("Warning: bitops.lua not found. The web build will not function correctly.");
                        Log("Please ensure bitops.lua is in the vendor directory.");
                    }
                    else if (fileExists("Balatro/bitops.lua"))
                    {
                        Log("Found bitops.lua already in Balatro directory.");
                    }
                }
                
                compressZip("Balatro/.", "balatro.zip");

                if (!fileExists("balatro.zip"))
                {
                    Log("Failed to pack Balatro folder!");
                    Exit();
                }

                Log("Moving archive...");
                if (_androidBuild)
                    fileCopy("balatro.zip", "balatro-apk/assets/game.love");

                if (_iosBuild)
                    fileCopy("balatro.zip", "game.love");

                if (_webBuild)
                {
                    // For web build, make sure we include bitops.lua in the game.love file
                    // Don't use the balatro.zip directly, but create a special game.love for web
                    Log("Creating game.love for web build...");
                    fileCopy("balatro.zip", "game.love");
                }
                #endregion

                if (_androidBuild)
                {
                    #region Packing APK
                    Log("Repacking APK...");
                    useTool(ProcessTools.Java, "-jar -Xmx1G -Duser.language=en -Dfile.encoding=UTF8 -Djdk.util.zip.disableZip64ExtraFieldValidation=true -Djdk.nio.zipfs.allowDotZipEntry=true \"apktool.jar\" b -o balatro.apk balatro-apk");

                    if (!fileExists("balatro.apk"))
                    {
                        Log("Failed to pack Balatro apk!");
                        Exit();
                    }
                    #endregion

                    #region Signing APK
                    Log("Signing APK...");
                    useTool(ProcessTools.Java, "-jar uber-apk-signer.jar -a balatro.apk");

                    if (!fileExists("balatro-aligned-debugSigned.apk"))
                    {
                        Log("Failed to sign APK!");
                        Exit();
                    }

                    Log("Renaming unsigned apk...");
                    fileMove("balatro.apk", "balatro-unsigned.apk");

                    Log("Renaming signed apk...");
                    fileMove("balatro-aligned-debugSigned.apk", "balatro.apk");
                    #endregion
                }

                if (_iosBuild)
                {
                    #region Packing IPA
                   
                    Log("Repacking iOS app...");
                    ModifyZip();

                    fileMove("balatro-base.zip", "balatro.ipa");
                    #endregion
                }

                if (_webBuild)
                {
                    #region Web Build
                    Log("Building Web version...");
                    if (directoryExists("web-build"))
                    {
                        Log("web-build directory already exists! Deleting web-build directory...");
                        tryDelete("web-build");
                    }

                    // Create output directory
                    System.IO.Directory.CreateDirectory("web-build");

                    // Check if love.js directory exists
                    if (!directoryExists("vendor/love.js"))
                    {
                        Log("vendor/love.js directory not found! Please ensure you have the love.js module installed.");
                        
                        // Create directory structure if it doesn't exist
                        Log("Creating vendor/love.js directory structure...");
                        if (!directoryExists("vendor"))
                        {
                            System.IO.Directory.CreateDirectory("vendor");
                        }
                        
                        System.IO.Directory.CreateDirectory("vendor/love.js");
                        
                        Log("You need to download love.js from https://github.com/Davidobot/love.js");
                        Log("Clone it to the vendor/love.js directory with: git clone https://github.com/Davidobot/love.js vendor/love.js");
                        Log("Or download the ZIP and extract it to vendor/love.js");
                        Log("Skipping web build...");
                        goto WebBuildEnd;
                    }

                    // Check if Node.js is installed
                    try
                    {
                        Process nodeCheck = new Process();
                        nodeCheck.StartInfo.FileName = "node";
                        nodeCheck.StartInfo.Arguments = "--version";
                        nodeCheck.StartInfo.UseShellExecute = false;
                        nodeCheck.StartInfo.RedirectStandardOutput = true;
                        nodeCheck.StartInfo.CreateNoWindow = true;
                        if (!nodeCheck.Start())
                        {
                            Log("Node.js is not installed. Please install Node.js to build the web version.");
                            Log("You can download it from https://nodejs.org/");
                            Log("Skipping web build...");
                            goto WebBuildEnd;
                        }
                        string nodeVersion = nodeCheck.StandardOutput.ReadToEnd().Trim();
                        Log($"Found Node.js {nodeVersion}");
                        nodeCheck.WaitForExit();
                    }
                    catch
                    {
                        Log("Node.js is not installed. Please install Node.js to build the web version.");
                        Log("You can download it from https://nodejs.org/");
                        Log("Skipping web build...");
                        goto WebBuildEnd;
                    }

                    // Check if the npm command exists
                    string npmPath = "npm"; // Default npm command
                    bool npmInPath = true;
                    try
                    {
                        Process npmCheck = new Process();
                        npmCheck.StartInfo.FileName = "npm";
                        npmCheck.StartInfo.Arguments = "--version";
                        npmCheck.StartInfo.UseShellExecute = false;
                        npmCheck.StartInfo.RedirectStandardOutput = true;
                        npmCheck.StartInfo.CreateNoWindow = true;
                        
                        if (!npmCheck.Start())
                        {
                            npmInPath = false;
                            Log("npm is not found in PATH despite Node.js being installed.");
                            
                            // Try to find npm in the default Node.js installation directory
                            string defaultNpmPath = @"C:\Program Files\nodejs\node_modules\npm\bin\npm-cli.js";
                            if (fileExists(defaultNpmPath))
                            {
                                Log("Found npm at default location: " + defaultNpmPath);
                                npmPath = "node \"" + defaultNpmPath + "\"";
                            }
                            else
                            {
                                Log("Looking for npm in Node.js installation directory...");
                                if (directoryExists(@"C:\Program Files\nodejs"))
                                {
                                    // Check if npm-cli.js exists in different locations
                                    string[] possiblePaths = {
                                        @"C:\Program Files\nodejs\node_modules\npm\bin\npm-cli.js",
                                        @"C:\Program Files\nodejs\npm.cmd",
                                        @"C:\Program Files\nodejs\node_modules\npm\bin\npm.cmd"
                                    };
                                    
                                    foreach (string path in possiblePaths)
                                    {
                                        if (fileExists(path))
                                        {
                                            Log("Found npm at: " + path);
                                            if (path.EndsWith(".js"))
                                            {
                                                npmPath = "node \"" + path + "\"";
                                            }
                                            else
                                            {
                                                npmPath = "\"" + path + "\"";
                                            }
                                            
                                            npmInPath = true;
                                            break;
                                        }
                                    }
                                }
                                
                                if (!npmInPath)
                                {
                                    Log("Please make sure npm is properly installed with your Node.js installation.");
                                    Log("Skipping web build...");
                                    goto WebBuildEnd;
                                }
                            }
                        }
                        else
                        {
                            string npmVersion = npmCheck.StandardOutput.ReadToEnd().Trim();
                            Log($"Found npm {npmVersion}");
                            npmCheck.WaitForExit();
                        }
                    }
                    catch
                    {
                        npmInPath = false;
                        Log("npm is not found in PATH despite Node.js being installed.");
                        
                        // Try using the default Node.js installation directory
                        string defaultNpmPath = @"C:\Program Files\nodejs\node_modules\npm\bin\npm-cli.js";
                        if (fileExists(defaultNpmPath))
                        {
                            Log("Found npm at default location: " + defaultNpmPath);
                            npmPath = "node \"" + defaultNpmPath + "\"";
                            npmInPath = true;
                        }
                        else 
                        {
                            Log("Using fallback npm path at C:\\Program Files\\nodejs\\node_modules\\npm");
                            npmPath = "node \"C:\\Program Files\\nodejs\\node_modules\\npm\\bin\\npm-cli.js\"";
                            npmInPath = true;
                        }
                        
                        if (!npmInPath)
                        {
                            Log("Please make sure npm is properly installed with your Node.js installation.");
                            Log("Skipping web build...");
                            goto WebBuildEnd;
                        }
                    }

                    // Check if package.json exists in the love.js directory
                    if (!fileExists("vendor/love.js/package.json"))
                    {
                        Log("package.json not found in vendor/love.js directory.");
                        Log("This suggests the love.js repository was not properly cloned or downloaded.");
                        Log("Please clone the repository with: git clone https://github.com/Davidobot/love.js vendor/love.js");
                        Log("Skipping web build...");
                        goto WebBuildEnd;
                    }

                    // Install npm dependencies for love.js
                    Log("Installing npm dependencies for love.js...");
                    try
                    {
                        Log("Running: " + npmPath + " install in vendor/love.js directory");
                        
                        // Change to love.js directory, run npm install, then return to original directory
                        string currentDir = System.IO.Directory.GetCurrentDirectory();
                        System.IO.Directory.SetCurrentDirectory(System.IO.Path.Combine(currentDir, "vendor", "love.js"));
                        
                        // Run npm install using Process
                        Process process = new Process();
                        
                        if (npmPath.StartsWith("node "))
                        {
                            // If we're using node to run npm-cli.js
                            string[] npmPathParts = npmPath.Split(new[] { ' ' }, 2);
                            process.StartInfo.FileName = npmPathParts[0]; // "node"
                            process.StartInfo.Arguments = npmPathParts[1] + " install"; // "path/to/npm-cli.js install"
                        }
                        else
                        {
                            // If we're using npm directly
                            process.StartInfo.FileName = npmPath;
                            process.StartInfo.Arguments = "install";
                        }
                        
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.RedirectStandardError = true;
                        
                        Log("Executing: " + process.StartInfo.FileName + " " + process.StartInfo.Arguments);
                        
                        bool success = process.Start();
                        if (!success)
                        {
                            Log("Failed to start npm process.");
                            System.IO.Directory.SetCurrentDirectory(currentDir);
                            goto WebBuildEnd;
                        }
                        
                        string output = process.StandardOutput.ReadToEnd();
                        string error = process.StandardError.ReadToEnd();
                        process.WaitForExit();
                        
                        // Return to original directory
                        System.IO.Directory.SetCurrentDirectory(currentDir);
                        
                        if (process.ExitCode != 0)
                        {
                            Log("Failed to install npm dependencies. Error: " + error);
                            if (!string.IsNullOrEmpty(output))
                                Log("Output: " + output);
                            Log("Skipping web build...");
                            goto WebBuildEnd;
                        }
                        else
                        {
                            Log("npm dependencies installed successfully");
                            if (!string.IsNullOrEmpty(output) && _verboseMode)
                                Log("Output: " + output);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log("Error running npm install: " + ex.Message);
                        Log("Make sure npm is in your PATH and vendor/love.js directory exists.");
                        Log("Try running 'npm install' manually in the vendor/love.js directory.");
                        Log("Skipping web build...");
                        goto WebBuildEnd;
                    }

                    // Run love.js to convert game.love to a web version
                    Log("Running love.js to create web build...");
                    try
                    {
                        // Verify game.love has the patched bitops before proceeding
                        Log("Verifying game.love has the necessary bitops patch...");
                        bool gameHasBitops = false;
                        
                        try
                        {
                            using (System.IO.Compression.ZipArchive archive = System.IO.Compression.ZipFile.OpenRead("game.love"))
                            {
                                foreach (var entry in archive.Entries)
                                {
                                    if (entry.FullName == "bitops.lua")
                                    {
                                        Log("Confirmed bitops.lua is present in game.love");
                                        gameHasBitops = true;
                                        break;
                                    }
                                }
                                
                                if (!gameHasBitops)
                                {
                                    Log("Warning: bitops.lua is not present in game.love!");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log("Error checking game.love for bitops.lua: " + ex.Message);
                        }
                        
                        if (!gameHasBitops)
                        {
                            // Extract game.love, add bitops.lua, and repack
                            Log("Attempting to add bitops.lua to game.love...");
                            
                            if (directoryExists("temp-game"))
                            {
                                Log("temp-game directory already exists! Deleting temp-game directory...");
                                tryDelete("temp-game");
                            }
                            
                            System.IO.Directory.CreateDirectory("temp-game");
                            extractZip("game.love", "temp-game");
                            
                            if (fileExists("vendor/bitops.lua"))
                            {
                                Log("Copying vendor/bitops.lua to temp-game directory...");
                                fileCopy("vendor/bitops.lua", "temp-game/bitops.lua");
                                
                                // Patch main.lua if it exists
                                if (fileExists("temp-game/main.lua"))
                                {
                                    string mainLuaContent = System.IO.File.ReadAllText("temp-game/main.lua");
                                    if (mainLuaContent.Contains("require \"bit\""))
                                    {
                                        Log("Replacing 'require \"bit\"' with 'local bit = require \"bitops\"' in game.love");
                                        mainLuaContent = mainLuaContent.Replace("require \"bit\"", "local bit = require \"bitops\"");
                                        System.IO.File.WriteAllText("temp-game/main.lua", mainLuaContent);
                                    }
                                }
                                
                                // Backup original game.love
                                fileMove("game.love", "game.love.bak");
                                
                                // Create new game.love with bitops
                                compressZip("temp-game/.", "game.love");
                                Log("Created new game.love with bitops.lua");
                                gameHasBitops = true;
                            }
                            else
                            {
                                Log("Warning: Could not find vendor/bitops.lua for patching!");
                            }
                            
                            tryDelete("temp-game");
                        }
                        
                        if (!gameHasBitops)
                        {
                            Log("Warning: Could not add bitops.lua to game.love. Web build may not function correctly.");
                        }
                        
                        // Copy game.love to vendor/love.js directory
                        Log("Copying game.love to vendor/love.js directory...");
                        if (fileExists("vendor/love.js/game.love"))
                        {
                            Log("Removing existing game.love in vendor/love.js directory...");
                            tryDelete("vendor/love.js/game.love");
                        }
                        fileCopy("game.love", "vendor/love.js/game.love");
                        
                        // Change to love.js directory, run love.js, then return to original directory
                        string currentDir = System.IO.Directory.GetCurrentDirectory();
                        string fullPathToWebBuild = System.IO.Path.Combine(currentDir, "web-build");
                        System.IO.Directory.SetCurrentDirectory(System.IO.Path.Combine(currentDir, "vendor", "love.js"));
                        
                        string nodePath = "node";
                        
                        Log("Executing: " + nodePath + " index.js game.love " + fullPathToWebBuild + " -c -t Balatro -m 600000000");
                        
                        Process process = new Process();
                        process.StartInfo.FileName = nodePath;
                        process.StartInfo.Arguments = "index.js game.love \"" + fullPathToWebBuild + "\" -c -t Balatro -m 600000000";
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.RedirectStandardError = true;
                        
                        bool success = process.Start();
                        if (!success)
                        {
                            Log("Failed to start node process for love.js.");
                            System.IO.Directory.SetCurrentDirectory(currentDir);
                            goto WebBuildEnd;
                        }
                        
                        string output = process.StandardOutput.ReadToEnd();
                        string error = process.StandardError.ReadToEnd();
                        process.WaitForExit();
                        
                        // Return to original directory
                        System.IO.Directory.SetCurrentDirectory(currentDir);
                        
                        if (!string.IsNullOrEmpty(output))
                            Log("love.js output: " + output);
                        
                        if (!string.IsNullOrEmpty(error))
                            Log("love.js error: " + error);
                    }
                    catch (Exception ex)
                    {
                        Log("Error running love.js: " + ex.Message);
                        goto WebBuildEnd;
                    }

                    WebBuildEnd:
                    ;
                    
                    // Skip success/failure checks if we jumped here due to an error
                    // that prevented the build from starting
                    bool webBuildAttempted = fileExists("vendor/love.js/game.love");
                    
                    if (webBuildAttempted)
                    {
                        if (!directoryExists("web-build") || !fileExists("web-build/index.html"))
                        {
                            Log("Failed to create web build!");
                            Log("Check if vendor/love.js directory exists and contains the necessary files.");
                            // Don't exit here, still allow other builds to complete
                        }
                        else
                        {
                            Log("Web build created successfully at web-build/index.html");
                            Log("To play, host the web-build directory with a web server and open in a browser. Adjust the game.html file to change the bounds of the canvas.");
                        }
                    }
                    
                
                    #endregion
                }
                
                #endregion
            }
        }

        //TODO: Implement for OSX and Linux!!!
        if ((!_iosBuild || _androidBuild || !_webBuild) && Platform.isWindows)
        {
            #region Android options
            #region Auto-install
            if (fileExists("balatro.apk") && AskQuestion("Would you like to automaticaly install balatro.apk on your Android device?"))
            {
                PrepareAndroidPlatformTools();

                Log("Attempting to install. If prompted, please allow the USB Debugging connection on your Android device.");

                useTool(ProcessTools.ADB, "install balatro.apk");
                useTool(ProcessTools.ADB, "kill-server");
            }
            #endregion

            #region Save transfer

            if (directoryExists(Environment.GetEnvironmentVariable("AppData") + "\\Balatro") && AskQuestion("Would you like to transfer saves from your Steam copy of Balatro to your Android device?"))
            {
                Log("Thanks to TheCatRiX for figuring out save transfers!");

                PrepareAndroidPlatformTools();

                Log("Attempting to transfer saves. If prompted, please allow the USB Debugging connection on your Android device.");

                useTool(ProcessTools.ADB, "shell mkdir /data/local/tmp/balatro");
                useTool(ProcessTools.ADB, "shell mkdir /data/local/tmp/balatro/files");
                useTool(ProcessTools.ADB, "shell mkdir /data/local/tmp/balatro/files/save");
                useTool(ProcessTools.ADB, "shell mkdir /data/local/tmp/balatro/balatro/files/save/game");
                useTool(ProcessTools.ADB, "push \"" + Platform.getGameSaveLocation() + "\\.\" /data/local/tmp/balatro/files/save/game");
                useTool(ProcessTools.ADB, "shell am force-stop com.unofficial.balatro");
                useTool(ProcessTools.ADB, "shell run-as com.unofficial.balatro cp -r /data/local/tmp/balatro/files .");
                useTool(ProcessTools.ADB, "shell rm -r /data/local/tmp/balatro");
                useTool(ProcessTools.ADB, "kill-server");
            }
            else
            {
                if (AskQuestion("Would you like to pull saves from your Android device?"))
                {
                    Log("Warning! If Steam Cloud is enabled, it will overwrite the save you transfer!");
                    while (!AskQuestion("Have you backed up your saves?"))
                        Log("Please back up your saves! I am not responsible if your saves get deleted!");

                    PrepareAndroidPlatformTools();

                    Log("Backing up your files...");
                    if (!directoryExists(Platform.getGameSaveLocation() + "BACKUP"))
                        System.IO.Directory.CreateDirectory(Platform.getGameSaveLocation() + "BACKUP/");
                    //TODO: No xcopy
                    RunCommand("xcopy", "\"" + Platform.getGameSaveLocation() + "\" \"" + Platform.getGameSaveLocation() + "BACKUP\\\" /E /H /Y /V");
                    tryDelete(Platform.getGameSaveLocation());
                    System.IO.Directory.CreateDirectory(Platform.getGameSaveLocation());

                    Log("Attempting to pull save files from Android device.");

                    //This sure isn't pretty, but it should work!
                    useTool(ProcessTools.ADB, "shell rm -r /data/local/tmp/balatro");
                    useTool(ProcessTools.ADB, "shell mkdir /data/local/tmp/balatro/");
                    useTool(ProcessTools.ADB, "shell mkdir /data/local/tmp/balatro/files/");
                    useTool(ProcessTools.ADB, "shell mkdir /data/local/tmp/balatro/files/1/");
                    useTool(ProcessTools.ADB, "shell mkdir /data/local/tmp/balatro/files/2/");
                    useTool(ProcessTools.ADB, "shell mkdir /data/local/tmp/balatro/files/3/");
                    useTool(ProcessTools.ADB, "shell touch /data/local/tmp/balatro/files/settings.jkr");
                    useTool(ProcessTools.ADB, "shell \"run-as com.unofficial.balatro cat files/save/game/settings.jkr > /data/local/tmp/balatro/files/settings.jkr\"");
                    useTool(ProcessTools.ADB, "shell touch /data/local/tmp/balatro/files/1/profile.jkr");
                    useTool(ProcessTools.ADB, "shell \"run-as com.unofficial.balatro cat files/save/game/1/profile.jkr > /data/local/tmp/balatro/files/1/profile.jkr\"");
                    useTool(ProcessTools.ADB, "shell touch /data/local/tmp/balatro/files/1/meta.jkr");
                    useTool(ProcessTools.ADB, "shell \"run-as com.unofficial.balatro cat files/save/game/1/meta.jkr > /data/local/tmp/balatro/files/1/meta.jkr\"");
                    useTool(ProcessTools.ADB, "shell touch /data/local/tmp/balatro/files/1/save.jkr");
                    useTool(ProcessTools.ADB, "shell \"run-as com.unofficial.balatro cat files/save/game/1/save.jkr > /data/local/tmp/balatro/files/1/save.jkr\"");
                    useTool(ProcessTools.ADB, "shell touch /data/local/tmp/balatro/files/2/profile.jkr");
                    useTool(ProcessTools.ADB, "shell \"run-as com.unofficial.balatro cat files/save/game/2/profile.jkr > /data/local/tmp/balatro/files/2/profile.jkr\"");
                    useTool(ProcessTools.ADB, "shell touch /data/local/tmp/balatro/files/2/meta.jkr");
                    useTool(ProcessTools.ADB, "shell \"run-as com.unofficial.balatro cat files/save/game/2/meta.jkr > /data/local/tmp/balatro/files/2/meta.jkr\"");
                    useTool(ProcessTools.ADB, "shell touch /data/local/tmp/balatro/files/2/save.jkr");
                    useTool(ProcessTools.ADB, "shell \"run-as com.unofficial.balatro cat files/save/game/2/save.jkr > /data/local/tmp/balatro/files/2/save.jkr\"");
                    useTool(ProcessTools.ADB, "shell touch /data/local/tmp/balatro/files/3/profile.jkr");
                    useTool(ProcessTools.ADB, "shell \"run-as com.unofficial.balatro cat files/save/game/3/profile.jkr > /data/local/tmp/balatro/files/3/profile.jkr\"");
                    useTool(ProcessTools.ADB, "shell touch /data/local/tmp/balatro/files/3/meta.jkr");
                    useTool(ProcessTools.ADB, "shell \"run-as com.unofficial.balatro cat files/save/game/3/meta.jkr > /data/local/tmp/balatro/files/3/meta.jkr\"");
                    useTool(ProcessTools.ADB, "shell touch /data/local/tmp/balatro/files/3/save.jkr");
                    useTool(ProcessTools.ADB, "shell \"run-as com.unofficial.balatro cat files/save/game/3/save.jkr > /data/local/tmp/balatro/files/3/save.jkr\"");
                    useTool(ProcessTools.ADB, "shell find /data/local/tmp/balatro/files/ -maxdepth 2 -size 0c -exec rm '{}' \\;");
                    useTool(ProcessTools.ADB, "pull /data/local/tmp/balatro/files/. \"" + Platform.getGameSaveLocation() + "\"");

                    useTool(ProcessTools.ADB, "kill-server");
                }
            }
            #endregion
            #endregion
        }

        Log("Finished!");
        Exit();
    }

    public static void Cleanup()
    {
        if (_cleaup)
        {
            Log("Deleting temporary files...");

            tryDelete("love-11.5-android-embed.apk");
            tryDelete("Balatro-APK-Patch.zip");//TODO: remove when Android build changes
            //tryDelete("AndroidManifest.xml");//TODO: enable when Android build changes
            tryDelete("apktool.jar");
            tryDelete("uber-apk-signer.jar");
            tryDelete("openjdk.zip");
            tryDelete("openjdk.tar.gz");
            tryDelete("openjdk");
            tryDelete("balatro-aligned-debugSigned.apk.idsig");
            tryDelete("balatro-unsigned.apk");
            tryDelete("platform-tools.zip");
            tryDelete("ios.py");
            tryDelete("balatro.zip");
            tryDelete("game.love");
            tryDelete("game.love.bak");
            tryDelete("temp-game");

            tryDelete("platform-tools");
            tryDelete("jdk-21.0.3+9");
            tryDelete("Balatro-APK-Patch");//TODO: remove when Android build changes
            //tryDelete("icons");//TODO: enable when Android build changes
            tryDelete("Balatro");
            tryDelete("balatro-apk");
            if (!gameProvided)
                tryDelete("Balatro.exe");
        }
    }

    /// <summary>
    /// Prepare Android platform-tools, and prompt user to enable USB debugging
    /// </summary>
    void PrepareAndroidPlatformTools()
    {
        //Check whether they already exist
        if (!directoryExists("platform-tools"))
        {
            Log("Platform tools not found...");

            if (!fileExists("platform-tools.zip"))
                TryDownloadFile("platform-tools", PlatformToolsLink, "platform-tools.zip");

            Log("Extracting platform-tools...");
            extractZip("platform-tools.zip", "platform-tools");
        }

        //Prompt user
        while (!AskQuestion("Is your Android device connected to the host with USB Debugging enabled?"))
            Log("Please enable USB Debugging on your Android device, and connect it to the host.");
    }
}
