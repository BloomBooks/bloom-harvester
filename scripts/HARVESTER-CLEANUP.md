# Harvester Machine Cleanup

## Temp folder

Since switching to WebView2, junk folders have been accumulating in the Temp folders of the
user accounts that run the harvester process.  These folders have names like "Bloom WV2-8089*"
and contain the user data for the various browsers that Bloom spins up.  They can easily have
over 10MB apiece, and can quickly grow to consume gigabytes of disk space.  Code has been
written recently to remove those folders when Bloom quits, but various crashes and other
problems may keep that code from running.

## BloomHarvester folder

The harvester program never deletes any book files that it downloads for processing.  Instead,
it leaves them in the folder %USERPROFILE%/AppData/Local/BloomHarvester/Prod (or the same path
but ending with /Dev for the development harvester).  Essentially every book in Bloom Library
is stored in this folder, meaning over 20,000 subfolders exist.  As people delete books after
uploading them, the folders stay on the harvester machine because there's no automatic way to
detect that happening.  (It's easy to poll for "what's new?" but polling for "what has been
deleted?" is a totally different question that cannot be answered.)  This calls for a periodic
manual pruning of the cached data.

The subfolder names in the BloomHarvester/Prod (or BloomHarvester/Dev) folder are just the book
ids assigned by the online Parse database.  This simplifies the task of identifying books that
have been deleted.  The cleanup job goes like this:

1. Turn off the harvester process.
2. Get the current list of book ids from the production (or development) parse server.
3. Scan all of the folder names in the BloomHarvester/Prod (or BloomHarvester/Dev) folder, and
   delete the folder if its name cannot be found in the downloaded list of ids.
4. Turn the harvester process back on.

Steps 2 and 3 are done automatically by the shell scripts `cleanupProduction.sh` and
`cleanupDev.sh`.  The scripts may need to be adjusted slightly when the harvester setup is
moved to a different machine depending on user names and directory structure.

Note that you'll need to get the secret keys set into the environment variables
`BloomHarvesterParseAppIdProd` and `BloomHarvesterParseAppIdDev` before starting this task.
You'll also need to install a bash environment such as the git bash shell window or the cygwin
bash shell window.  With the cygwin environment, you may need to convert line endings from CRLF
to just LF.

The `df` command is included in these scripts before and after each cleanup so that you can see
how much disk space has been reclaimed.  You could also use `ls | wc` on the Prod or Dev
folders to see how many subfolders exist.  (The latter might take a minute or two for the Prod
folder which has over 20,000 subfolders.)
