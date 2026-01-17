EXTERNAL SetEventVar(varName, value)

VAR player_mean = 0
VAR player_scared = 0
VAR player_stupid = 0
VAR SpiritAngered = 0
VAR GraveRobberSetup = 0

=== start ===
Yo, you work here?
Listen man, can't hang around here too long.
I don't want to come in.
Just came here to give you a headsup.

+ [Who are you?]
    -> who_are_you
+ [A headsup?]
    -> headsup
    
=== headsup ===
Yeah, man.
Listen up, my brother has been acting strange lately.
Said something about a gig involving this place.
Goddamn cops wouldn't listen. They don't take me seriously.
Anyway, I just came to warn you.
If my brother stops by to "visit" a grave...
Do NOT let him in.
Got that?

+ [Why not?]
    -> why_not
+ [Alright, I understand. Can you give me anymore details?]
    -> understood
    
=== why_not ===
I already told you man.
He's been hanging around the wrong people.
It's nothing but trouble. Don't want to see him get in any more trouble than he's already in.
Just send him back man. Don't let him in and don't let him convince you otherwise.
Him and that weird cult-like gang he's been hanging around lately... I don't know.
Somethin' is off.

+ [Okay. Thanks for the warning.]
    -> okay
+ [Sorry, but I'll be handling the security myself. You should go home.]
    -> declined
    
    
=== who_are_you ===
My brother and I are part of the Deloitte family. We live a few miles down the road.
My names Reginald and he's Shane.
I don't want anything from ya'. I just want to stop my brother from doing something stupid, that he might regret.
He's always getting himself into trouble.
Can you promise you won't let him or his gang in?

    + [Okay. Thanks for the warning.]
    -> okay
+ [Sorry, but I'll be handling the security myself. You should go home.]
    -> declined
    
    
=== understood ===
Thanks for listening man.
We're part of the Deloitte family. We don't live too far from here.
My brother looks a lot like me. A bit shorter, but not by much.
Him and his weird gang have been participating in a lot of cult-like activities. He's hangin' around these folks dressing in all black, running around causing all sorts of problems.
Couldn't quite make out what he said before he took off, something about this graveyard being important.
I think he's up to no good. Probably wants to mess with some of the things burried.
Do NOT let him in. I don't want to see him ending up in even more trouble.
Can you promise me that?

    + [Okay. Thanks for the warning.]
    -> okay
+ [Sorry, but I'll be handling the security myself. You should go home.]
    -> declined


=== okay ===
Thanks man.
I owe you one.
-> END


=== declined ===
I ain't surprised man.
None of y'all take me seriously.
I hope y'all get what's coming to ya'.
-> END
