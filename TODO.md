When a new Umbraco version is released...

* [x] Download release ZIP from Our Umbraco
* [x] Create folder for specific version (`/releases/7.1.8/`)
* [x] Create MD5 of zip
* [x] Create MD5 of zip entries
* [x] Extract assembly version numbers (along with MD5 hashes)
* [x] Extract the date/time of the zip release (using the first zip entry)
* [x] Add to the HTML listing
* [x] FTP files...
	* [x] Upload ZIP archive
	* [x] Diff/patch files
	* [x] Upload HTML listing

* [x] Create patch diff zip with the previous version?
* [x] Compare release with previous version - export diffs to HTML (or other)

* [x] Maintain a list of releases - dates, etc
	* [x] Two files in the root of the folder:
		* [x] releases.json
		* [x] latest.json

---
