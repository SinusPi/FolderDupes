# File duplicates from a folder's perspective

Typical file comparison tools scan drives for duplicate files and report the results in a single list. This is useful if you know which folders you want to keep and which contain useless duplicates, and there's not a lot of them. However, if you happen to have folders overlapping with 90% of files, and you're not sure which are newer, you'll likely want to take a closer look at the folder contents, and a simple list of duplicates won't help much. That's where FolderDupes comes in, presenting the duplicate information from a folder-centric perspective.

In the "broad" mode, FolderDupes scans a target folder tree and reports how unique or duplicate-heavy each folder is, presenting its "cousins" for further inspection.

In a "focused" mode, again the target tree is scanned, but only the designated folder is listed out, each of its file annotated whether it's unique or has duplicates elsewhere, and if so, where. Also, if you use any content-based comparison mode, near-duplicates (matched by name/size/date, but not content) are also listed as possibly changed/damaged copies.

Files can be compared by name, size, date, or full content or EXIF date for JPGs. Content inspection is performed only when other modes are a match.

## CLI options

-i \<folder\> : add folder for scanning. Scan depth is infinite. Repeatable.

-x \<pattern\>: add an exclusion pattern; repeatable. Defaults: \\node_modules, \\.git, \\.svn, .picasa.ini, Thumbs.db, desktop.ini. Use '-x !' to clear those.

-m \<mode\>: compare mode flags: n = name, d = date, s = size, c = content, e = “ends”/fast but weak hash, x = EXIF date for JPGs. Default is "ns" for name and size.

-f \<folder\>: Engage "focused" mode, listing one folder's files only.

--only-dupes: in focused mode, skip unique files.

-v: verbose progress output.

--help: show usage.

## GUI

In the works. A two-pane "commander" window would be nice. One day.