# Eternal Package Checker

## Eternal.PackageCheck NuGet library package

Copyright Eternal Developments, LLC. All Rights Reserved.

## License

MIT

## Functionality

This utility checks the meta data associated with a given package and optional version. It also lists all the first order dependencies with their licenses, and
any CVEs found for the package using https://osv.dev/ and the https://nvd.nist.gov/ APIs.
The dependency pins are properly respected e.g. if a package depends on a specific version of another package, that will be reported correctly.
This can be helpful when filling in corporate approval forms and when you wish to make sure all the correct fields are set in your published package.

Installation for Windows 11 and Ubuntu Raccoon: `dotnet tool install --global Eternal.PackageCheck`

It supports the following four ecosystems:

1. RubyGems e.g. rubygems bcrypt
2. NuGet e.g. nuget Eternal.ConsoleUtilities
3. Go e.g. go golang.org/x/sys
4. npm e.g. npm is-obj
5. Python e.g. pypi hypercorn
6. Cargo e.g. cargo serde
7. Debian e.g. deb libcurl4t64

Debian packages are looked up in the Debian archive (https://api.ftp-master.debian.org/ and https://snapshot.debian.org/). With no version, the version in
stable is used. The licenses and copyright come from the 'Files: *' paragraph of the package's machine-readable copyright file; a package with a free-form
copyright file reports no licenses. The dependencies listed are the source package's build dependencies, as the run time dependencies of a binary package are
not available through any API. A purl from a derivative distribution such as Ubuntu is still looked up in the Debian archive.
CVEs are looked up against the source package, and only for the Debian release the version belongs to (e.g. Debian:13), which is taken from the suite that
carries it or from a "+debNN" or "~bpoNN" suffix in its version. An older version that can be placed in no release reports CVEs from every release.

It is designed to be strict when detecting licenses - please let me know if you find a false positive.

The utility makes some GitHub and GitLab API calls. For those you'll need to set up a Personal Access Token for each provider and set an environment variable to each one.

`GITHUB_PAT=github_pat_11BF*************D5xsl_gkQeLZidP2t*****************RM2NVZ****gfl`

`GITLAB_PAT=glpat-pkD8J**********EV9WJfGM**********TpvdjFheA8.01.170****lv`
 
## Looking up the license from a Repo
 
1. The root folder (and only the root folder) of the repo is interrogated for a list of files.
2. Any file that has 'license', 'licence', 'COPYING', 'Copyright', or 'BSDL' is a candidate file. The name check is case insensitive.
	a. e.g. These files will be determined to be candidate files 'MIT-LICENSE', 'License.txt', 'BSD-Licence'
3. The text version of the file (not the decorated HTML version) is downloaded.
	a. The file contents are sanitized.
	b. This means all carriage returns, new lines, and tabs are converted to spaces.
	c. Any smart quotes are replaced with vanilla quotes.
	d. All double spaces are converted to single spaces.
4. The starting words and the ending words of the license are searched for in a case insensitive fashion.
5. If both the above exist, then key phrases from the license are looked for.
	a. This is to account for when the license is changed (for example) from 'AUTHORS' to 'Company, LLC'
	b. If all the phrases are found, then the license matches and the HTML link is set.
6. The pre-sanitized license contents are stored in the TPS file.
 
## Looking up the copyright from the license contents

1. The license and license contents are validated to ensure consistency.
2. All words between 'Copyright' and the start of the license boilerplate text are extracted.
3. This is sanitized and set as the copyright message.

## Licenses that are detected

a.k.a. Licenses with the meta data setup

The meta data is externalized - you can edit LicenseBoilerPlate.json and LicensePriorities.json to add new licenses.

MIT
ISC
MIT-0 
BSD-3-Clause
BSD-2-Clause
0BSD
Apache-2.0
PSF-2.0
BlueOak-1.0.0
Ms-PL
MPL-2.0 - no copyright extraction
CC0-1.0 - no copyright extraction
CC-BY-4.0 - no copyright extraction
Ruby - converted to the BSD-2-Clause
WTFPL
Hippocratic-2.1
LGPL-2.0-only - WIP
LGPL-2.0-or-later - WIP
LGPL-2.1-only - WIP
LGPL-2.1-or-later - WIP
LGPL-3.0-only - WIP
LGPL-3.0-or-later - WIP
GPL-2.0-only - WIP
GPL-2.0-or-later - WIP
GPL-3.0-only - WIP
GPL-3.0-or-later - WIP
RPL-1.5 - WIP
