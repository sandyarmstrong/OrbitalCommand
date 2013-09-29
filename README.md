# Orbital Command

Automating some tasks in git when my bash skills don't cut it.

First example: extracting bug numbers from commit messages to help
me write release notes.

Suppose your team creates commit messages like this:

    My short commit message
    
    Longer description of the commit, paragraphs, etc.
    
    Fixes: https://mybugtracker/?12345

Digging through the whole git log has gotten to be a somewhat painful
exercise in copy/paste. I end up making my release notes more
human-friendly, too, so now that process is mixed up in annoying
information retrieval.

Now, I just run `OrbitalCommand.exe previousreleasetagname` and I get:

    * Bump version to latesttagname
    * Fix upgrade bug ([12345][12345])
    * Change some low-level stuff
    * Fix several bugs with one underlying issue ([23456][23456], [34567][34567])
    
    [12345]: https://mybugtracker/?12345
    [23456]: https://mybugtracker/?23456
    [34567]: https://mybugtracker/?34567

This is Markdown! If I was really lazy, this already gives me:

* Bump version to latesttagname
* Fix upgrade bug ([12345][12345])
* Change some low-level stuff
* Fix several bugs with one underlying issue ([23456][23456], [34567][34567])

[12345]: https://mybugtracker/?12345
[23456]: https://mybugtracker/?23456
[34567]: https://mybugtracker/?34567

Now all I have to do is edit to be friendlier to QA and I have lovely Markdown
release notes. I use that as my annotation for the new release tag. When
I create the release build, the build machine uses the annotation as the body of
the Github Release it generates. That triggers a webhook event, and another
service (to be released soon I hope) shoots off an HTML email that includes
information including an HTMLized version of the Markdown release notes.

Now I'm happy. :-)
