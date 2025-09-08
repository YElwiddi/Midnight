VAR player_karma = 0
VAR quest_accepted = false

=== start ===
Hello there, traveler! I haven't seen you around these parts before.

+ [I'm just passing through.]
    -> passing_through
    
+ [I'm looking for adventure!]
    -> seeking_adventure

=== passing_through ===
Ah, a wanderer then. Well, be careful out there. 
The roads aren't as safe as they used to be.
~ player_karma = player_karma - 1
-> END

=== seeking_adventure ===
Adventure, you say? Well, you've come to the right place!
I have a quest that needs a brave soul like yourself.
~ player_karma = player_karma + 2
~ quest_accepted = true
-> END
