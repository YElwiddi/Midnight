VAR player_karma = 0

=== start ===
...
Are you just going to keep staring?

+ [Where the hell am I?]
    -> WhereAmI
    
+ [Who are you?]
    -> WhoAreYou

=== WhereAmI ===
Well, we seem to be in some sort of dark dungeon.

+ [Thanks. You're a lot of help.]
    -> Helpful
+ [I just woke up here. Can you show me the way out?]
    -> HelpMeOut

=== HelpMeOut ===
The only way out is the same way you came in!
Also, you'd better keep your voice down, friend.
    + [Why?]
    -> WhosWatching

=== WhoAreYou ===
Kind of you to ask.
But that's not important right now. She's watching and listening.
+ [Who's watching?]
    -> WhosWatching
    
=== WhosWatching ===
Ah, nevermind. I'm sure you'll be acquainted soon. Best of luck!
-> END


=== Helpful ===
No problem!
Next time, try asking nicely.
You won't get far with that attitude.
~ player_karma = player_karma - 1
-> END

