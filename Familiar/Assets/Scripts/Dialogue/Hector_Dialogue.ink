VAR player_mean = 0
VAR player_scared = 0
VAR player_stupid = 0

=== start ===
#speaker: Hector
Hey Brian! I came by to see how you've been.
I'm sorry you had to take the night shift here. This place couldn't look more depressing.

+ [It's not that bad...]
    -> brave_response
+ [I appreciate you coming by, Hector. It's good to see you.]
    ~player_mean = player_mean - 1
    -> friendly_response
    
=== brave_response ===
If you say so, man. I wouldn't want to be cooped up in that shanty cabin watching over those dead people.
...
Hey - don't get me wrong. A lot of people would kill to have a job like this. Not much to do around here but listen to the radio and doze off. Some people like that kind of stuff.
How are you keeping busy anyway?
+ [Honestly, it's been pretty dull so far.]
    -> bored_response
+ [Just been keeping my head down and staying vigilant, to be honest.]
    -> vigilant_response
    
=== bored_response ===
Dull? Well, if you say so. I guess that's better than being creeped out.
I can't stand the idea of sleeping a few feet away from those tombstones.
But hey, if you're that bored, why not make it a bit more interesting?
Have you heard the rumors about the ravaging wildebeast that roams the woods not too far from these parts?
How about I head home to grab some equipment, and then we go track it down together?
+ [No thanks, I really shouldn't leave my shift.]
    ~player_scared = player_scared + 1
    -> scared_response
+ [Real funny, Hector.]
    -> smart_response_2
    
=== scared_response ===
Heh, alright, whatever you say.
Well I just dropped by for a moment to check in on you.
I'll stop by again tomorrow to make sure you're alright.
Your folks were pretty worried about you, so I'll make sure to give them your regards. Hopefully that'll ease them a bit.
See you around man.
-> END

=== smart_response_2 ===
Hey man, just trying to help!
Well I just dropped by for a moment to check in on you.
I'll stop by again tomorrow to make sure you're alright.
Your folks were pretty worried about you, so I'll make sure to give them your regards. Hopefully that'll ease them a bit.
See you around man.
-> END

=== vigilant_response ===
Sounds boring.
This place kind of gives me the creeps.
In the middle of nowhere too. Why the hell is this place even here?
Feels a bit off, but I guess you're getting paid well at least, right?
+ [Yeah, the pay is great.]
    ~player_stupid = player_stupid + 1
    -> stupid_response
+ [I guess.]
    ~player_mean = player_mean + 1
    -> smart_response
    
=== friendly_response ===
It's good to see you too, man.
This place kind of gives me the creeps.
In the middle of nowhere too. Why the hell is this place even here?
Feels a bit off, but I guess you're getting paid well at least, right?
+ [Yeah, the pay is great.]
    ~player_stupid = player_stupid + 1
    -> stupid_response
+ [I guess.]
    ~player_mean = player_mean + 1
    -> smart_response

=== stupid_response ===
That's great to hear man!
I'm happy for you.
Well I just dropped by for a moment to check in on you.
I'll stop by again tomorrow to make sure you're alright.
Your folks were pretty worried about you, so I'll make sure to give them your regards. Hopefully that'll ease them a bit.
See you around man.
-> END

=== smart_response ===
Well, I should probably head back now. I just dropped by to check in on you.
Your folks were pretty worried about you, so I'll make sure to give them your regards. Hopefully that'll ease them a bit.
See you around man.
-> END