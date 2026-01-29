EXTERNAL SetEventVar(varName, value)

VAR player_mean = 0
VAR player_scared = 0
VAR player_stupid = 0
VAR SpiritAngered = 0
VAR GraveRobberSetup = 0
VAR GraveKeeperAngered = 0

=== start ===
#speaker: ???
Well, my friend...
It seems you've done a great job with this old place.

+ [Who are you?]
    -> who_are_you
+ [It's late, old man. Head home.]
    -> head_home

=== head_home ===
As astute as always.
Allow me to introduce myself, first.

-> continue_1

=== who_are_you ===
I think it's about time I introduced myself...
-> continue_1

=== continue_1 ===

I'm the previous watcher of this cemetary, before you.
I think I left this place in the right hands, but I also believe your job here isn't finished.

+ [Why did you leave?]
    -> why_leave
+ [Well, yeah. My shift isn't over.]
    -> sarcasm

=== why_leave ===
Well, I'll admit, I didn't uphold my duties as the grave watcher.
I felt there had to be someone more suited to the job...

-> continue_2

=== sarcasm ===
...
You've got quite the sense of humor there, friend...
No, I meant there's something I left unfinished before I left.

-> continue_2

=== continue_2 ===

As you might be able to tell by now, this job isn't as easy as it seems.
This place is quite the attraction to many different folks, both alive and dead.
I'll be candid with you, my friend...
I left because I was afraid.
This place isn't any normal resting place for the dead. I'd love to share more with you, if you'd be willing to listen...

+ [Okay.]
    -> listening
+ [You should head home grandpa.]
    -> angered
    
=== angered ===
Listen here.
I came here out of the pain of regret. Out of empathy for you and the people of this town.
I didn't have to come back, to put up with your arrogance.

+ [I'm sorry. Please continue.]
    -> listening
+ [Don't care. Head home, you crazy old bat.]
    -> wtf_side
    
=== wtf_side ===
You are a fool.
I will give you one last chance to listen to me.

+ [I'm sorry. Please continue. I'm listening.]
-> listening
+ [If you don't leave I'm calling the cops.]
-> stupid
    
=== listening ===
Thank you.
As you can tell, this place gets a lot of attention.
There is a story going around, that there is something very valuable in the mausoleum sitting right behind you. A lot of dangerous folks are after it.
I personally never cared for it.
However, as my days unfolded in this place, I began growing restless, tired, and anxious.
I started feel an unsettling draw towards the mausoleum sitting behind you.
Have you felt it too?

+ [Yes.]
-> yes
+ [No.]
-> continue_3

===yes===
Interesting.
It's relieving to actually meet someone that feels the same way.

-> continue_3

===continue_3===
Anyway, I needed answers so I began digging into the old notes that the previous gravekeepers had left for me within the cabin.
I'm sure you've had a chance to read some of the documentation that had been left there. At least, I hope you have.
As I sifted through the old text, I read an interesting passage that an old grave keeper left. Probably decades ago.
The documents had been sitting there for what seemed like a very long time... It was dusty and barely legible.
I had decided to leave this position at the graveyard promptly after I read it.
Apparently, one of the old grave watchers had entered that mausoleum, and was able to leave and record their findings.
In the text, they mentioned that there must always be a gravekeeper assigned to this position. Anyone who takes this job is responsible for the safety of the folks in this area. If you don't want this job, you have to pass it onto someone else.
However.
If you do decide to keep this job, you will learn that you will no longer be able to lead a normal life. Additionally, you will not be able to die normally either.
According to the old grave keeper, this graveyard is more than just a normal burial place. It requires a watcher at all times, even through death. 
When normal people die, their lives end and they no longer have any experiences, thoughts, or feelings.
The texts didn't elaborate on this, but if you die here, as the gravekeeper, that isn't exactly what will happen to you.
The only way to break this cycle is to enter the mausoleum. I don't know why or how, but the old gravekeeper mentioned there was some sort of puzzle that needed to be solved.

+ [What puzzle?]
-> what_puzzle


===what_puzzle===
...
Everyone who is burried at this graveyard shares something in common.
Some of these graves are centuries old. However, even the newer ones that were placed here more recently are following a pattern.
I have not been able to figure out what this pattern is, or if it even exists.
The only way to break this cycle is solve this puzzle. After you've learned it, you must enter the mausoleum.
When I learned about this, I was in disbelief. I had thought this place was just cursed.
I burned the old keeper's note, locked the mausoleum and the gate, and then left this place.
I still have the key. I can unlock that crypt.
Will you enter it and put this to an end?

+ [Okay. I'm ready.]
-> ready
+ [This is crazy. Go home you old kook.]
-> wtf_1

=== wtf_1 ===
I am quite disappointed.
Surely you are joking?

+ [Yeah, just a joke. Go ahead and unlock the crypt.]
-> joke
+ [That wasn't a joke. Please leave.]
-> wtf_2


=== wtf_2 ===
I was mistaken.
You clearly are not the right person for this job.
I will warn you one last time to listen to me.
It is your well-being I am concerned about.

+ [I'm sorry. Please go ahead and unlock the crypt]
-> joke
+ [If you don't leave I'm calling the cops.]
-> stupid

=== stupid ===
You are quite the intelligent specimen, aren't you?
~ SetEventVar("allowed_inside", false)
~ GraveKeeperAngered = GraveKeeperAngered + 1
->END

=== joke ===
...
Follow me.
~ SetEventVar("allowed_inside", true)
-> END

=== ready ===
You truly are a blessing.
Follow me.
~ SetEventVar("allowed_inside", true)
-> END