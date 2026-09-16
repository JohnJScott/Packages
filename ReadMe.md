# Eternal Package Checker

## Eternal.PackageCheck NuGet library package

Copyright Eternal Developments, LLC. All Rights Reserved.

## License

MIT

## Functionality

This utility checks the meta data associated with a given package and optional version. This can be helpful when filling in corporate approval forms and when you wish to make sure all the correct fields are set in your published package.

It also lists any CVEs found for the package using https://osv.dev/ and the https://nvd.nist.gov/ APIs.

Installation for Windows 11 and Ubuntu Raccoon: `dotnet tool install --global Eternal.PackageCheck`

It supports the following four ecosystems:

1. RubyGems e.g. rubygems bcrypt
2. NuGet e.g. nuget Eternal.ConsoleUtilities
3. Go e.g. go golang.org/x/sys
4. npm e.g. npm is-obj
5. Python e.g. pypi hypercorn

Python will be next.

It is designed to be strict when detecting licenses - please let me know if you find a false positive.

The utility makes some GitHub and GitLab API calls. For those you'll need to set up a Personal Access Token for each provider and set an environment variable to each one.

`GITHUB_PAT=github_pat_11BF*************D5xsl_gkQeLZidP2t*****************RM2NVZ****gfl`

`GITLAB_PAT=glpat-pkD8J**********EV9WJfGM**********TpvdjFheA8.01.170****lv`
 
## Looking up the license from a Repo
 
1. The root folder (and only the root folder) of the repo is interrogated for a list of files.
2. Any file that has 'license', 'licence', 'COPYING', or 'Copyright' is a candidate file. The name check is case insensitive.
	a. e.g. These files will be determined to be candidate files 'MIT-LICENSE', 'License.txt', 'BSD-Licence'
3. The text version of the file (not the decorated HTML version) is downloaded.
	a. The file contents are sanitized.
	b. This means all carriage returns, new lines, and tabs are converted to spaces.
	c. Any smart quotes are replaced with vanilla quotes.
	d. Asterisks and numbered items (e.g. '2.') are removed to make various versions of the BSD license consistent.
	e. All double spaces are converted to single spaces.
4. The starting words and the ending words of the license are searched for in a case insensitive fashion.
5. If both the above exist, then key phrases from the license are looked for.
	a. This is to account for when the license is changed (for example) from 'AUTHORS' to 'Company, LLC'
	b. If all the phrases are found, then the license matches and the HTML link is set.
6. The pre-sanitized license contents are stored or downloaded and stored.
 
## Looking up the copyright from the license contents

1. The license and license contents are validated to ensure consistency.
2. All words between 'Copyright' and the start of the license boilerplate text are extracted.
3. This is sanitized and set as the copyright message.

## Licenses that are detected

a.k.a. Licenses with the meta data setup

1. MIT
2. ISC
3. MIT-0 
4. BSD-3-Clause
5. BSD-2-Clause
6. 0BSD
7. Apache-2.0
8. PSF-2.0
9. BlueOak-1.0.0
10. MPL-2.0 - no copyright extraction
11. CC0-1.0 - no copyright extraction
12. CC-BY-4.0 - no copyright extraction
13. Ruby - use this license or the BSD-2-Clause instead
14. WTFPL
15. Hippocratic-2.1
16. LGPL-2.0-only - WIP
17. LGPL-2.0-or-later - WIP
18. LGPL-2.1-only - WIP
19. LGPL-2.1-or-later - WIP
20. LGPL-3.0-only - WIP
21. LGPL-3.0-or-later - WIP
22. GPL-2.0-only - WIP
23. GPL-2.0-or-later - WIP
24. GPL-3.0-only - WIP
25. GPL-3.0-or-later - WIP
