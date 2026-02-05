EXTERNAL SetEventVar(varName, value)
EXTERNAL SuspendDialogue()

VAR player_mean = 0
VAR player_scared = 0
VAR player_stupid = 0
VAR SpiritAngered = 0
VAR GraveRobberSetup = 0
VAR Sanity = 100
VAR GraveyardProtection = 100


=== start ===
#speaker: Tabitha
Good evening.
You must be the gravekeeper.

My name is Tabitha.
I was hoping you’d be on duty tonight.

+ [I'm not interested.]
    ~player_mean = player_mean + 1
    -> tabitha_direct
+ [Can I help you with something?]
    -> tabitha_friendly

=== tabitha_direct ===
#speaker: Tabitha
Curiosity keeps most people alive longer than confidence.
I ask for a mere moment.

I want to enter the graveyard.
Not to disturb anything.
To protect it.

There are… restless things.
You don’t see them.
But they notice places like this.

+ [That sounds like nonsense.]
    -> tabitha_dismiss
+ [Protect it how?]
    ~player_scared = player_scared + 1
    -> tabitha_explain

=== tabitha_friendly ===
#speaker: Tabitha
You have a kind voice.
That helps, in a place like this.

I work with old rites.
Small blessings.
They keep bad spirits from wandering too close.

This graveyard feels thin.
Like a door left unlocked.

+ [You expect me to believe that?]
    -> tabitha_dismiss
+ [What kind of rites?]
    ~player_scared = player_scared + 1
    -> tabitha_explain

=== tabitha_dismiss ===
#speaker: Tabitha
You call it nonsense.
That’s fine.

Most people do.
Until something follows them home.

I don’t charge.
I don’t ask for thanks.
Just permission.

-> tabitha_request

=== tabitha_explain ===
#speaker: Tabitha
Nothing dramatic.
No candles. No blood. Just words said in the right places.

The dead sleep better after, and the living too.

You look tired.
That kind of tired doesn’t come from long shifts.

-> tabitha_request

=== tabitha_request ===
#speaker: Tabitha
You won’t owe me anything.
It will be a simple procedure. I'll walk the paths of the graveyard, and put the souls to rest.
You will immediately feel at ease.

-> ask


=== ask ===

Let me inside.



+ [Alright. Go ahead, but I’m watching you.]
     ~ SetEventVar("allowed_inside", true)
     ~SpiritAngered = SpiritAngered - 1
     ~GraveRobberSetup = GraveRobberSetup + 1
     ~GraveyardProtection = GraveyardProtection - 15
    -> tabitha_allow
+ [No. I can’t let you in.]
    ~player_mean = player_mean + 1
     ~ SetEventVar("allowed_inside", false)
    -> tabitha_deny
    + [I have a few more questions.]
    -> more_questions
  + [Hold on, I'll be right back.]
      ~ SuspendDialogue()
    -> ask
    
    
    === more_questions ===
    Ask away.
+ [What was your name again?]
    -> ask_name_again
+ [Why are you here?]
    -> why_here
+ [Do you have any family members?]
    -> how_many
+ [Nevermind.]
-> ask

===how_many===
Yes, unfortunately.
My pestiferous sister, Maribel. She may try to come here and undo my work. Or make things worse.
I trust you are fit for your job and will make the correct decisions.
+[I have other questions]
-> more_questions
+[Nevermind.]
-> ask
    
    === ask_name_again ===
    Tabitha.
+[I have other questions]
-> more_questions
+[Nevermind.]
-> ask
    ===why_here===
    I am here to ease the restless spirits in this graveyard.
    You don't need to be afraid...
    I am here to help.
    +[Why did you choose this graveyard?]
    -> why_this
    +[Are you attempting to deceive me?]
    -> are_you_lying
    +[I have other questions.]
    -> more_questions
    +[Nevermind.]
    ->ask
    
    ===why_this===
    Don't get the wrong idea, gravekeeper.
    I am merely a guardian, much like yourself. I happened to stumble upon a place that needed help, and you happened to be here.
    I offer my help. You may choose to accept it, or not.
    +[Are you attempting to deceive me?]
    -> are_you_lying
    +[I have other questions.]
    -> more_questions
    +[Nevermind.]
    ->ask
    
    ===are_you_lying===
    Deceive you?
    If I were lying, what good would it help to ask me that?
    I don't think you're thinking this through.
        +[Why did you choose this graveyard?]
    -> why_this
        +[I have other questions.]
    -> more_questions
    +[Nevermind.]
    ->ask
    
=== tabitha_allow ===
#speaker: Tabitha
Good.
You made the right choice.

If tonight feels quieter than usual, that’s me helping.

-> END

=== tabitha_deny ===
#speaker: Tabitha
I see.
You guard the gate, but not what slips through cracks.

Very well.
Don’t say I didn’t offer.

Sleep lightly tonight.

-> END