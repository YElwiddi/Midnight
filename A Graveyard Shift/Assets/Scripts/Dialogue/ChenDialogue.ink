EXTERNAL SetEventVar(varName, value)

VAR player_mean = 0
VAR player_scared = 0
VAR player_stupid = 0
VAR SpiritAngered = 0
VAR GraveRobberSetup = 0
VAR Sanity = 100
VAR GraveyardProtection = 100

=== start ===
#speaker: ???
…You work here?
I didn’t expect anyone still on duty.

-> chen_opening

=== chen_opening ===
+ [Are you here to visit someone?]
    -> chen_visit
+ [You shouldn’t be wandering around this late.]
    ~player_mean = player_mean + 1
    -> chen_defensive

=== chen_visit ===
#speaker: ???
Yeah. Just paying my respects.
I don't plan to stay long.

+ [Who are you visiting?]
    -> chen_evade
+ [Are you related to the deceased?]
    -> chen_family

=== chen_defensive ===
Thanks, I know the rules.
You don't have to worry, I’ll be quick.
Then you can go back to... whatever it is you were doing.

+ [Who are you visiting?]
    -> chen_evade
+ [Are you related to the deceased?]
    -> chen_family

=== chen_evade ===
Someone with the name Huang.
That should be enough for you.

+ [That’s vague. What’s your full name?]
    -> chen_name
+ [Are you related to the Huangs?]
    -> chen_reaction

=== chen_name ===
#speaker: Chen Huang
…Chen Huang.

Did you want my social security number too?

+ [Are you related to the deceased?]
    -> chen_reaction
+ [Alright. Why are you here?]
    -> chen_business

=== chen_family ===
#speaker: ???
Yeah. I am.
But to be honest, I don't really think that is any of your concern.

+ [I just need to know who exactly you're visiting.]
    -> chen_hint
+ [I just need to know you’re not causing trouble.]
    -> chen_business

=== chen_reaction ===
#speaker: Chen Huang
I figured you’d ask that.

Yes.
We’re family.

Not close.

+ [Well, I need to know who you're visiting.]
    -> chen_hint
+ [What is their name?]
    -> ask_name
    
=== ask_name ===
Mei Huang.

-> chen_close

=== chen_hint ===
#speaker: Chen Huang
Family.

+ [I need to know their name.]
    ~player_stupid = player_stupid - 1
    -> ask_name
+ [You’re avoiding the question.]
    ~player_mean = player_mean + 1
    -> chen_shut_down

=== chen_shut_down ===
#speaker: Chen Huang
I’m answering enough.
This isn’t a confession booth.

-> chen_close

=== chen_business ===
#speaker: Chen Huang
I’ll pay my respects and leave.
You won’t even notice I was here.

-> chen_request

=== chen_close ===
#speaker: Chen Huang
I'm not going to answer anymore questions.
-> chen_request

=== chen_request ===
#speaker: Chen Huang
Is that going to be a problem?

+ [No. Go ahead inside, but be quick.]
    -> chen_allow
+ [Visiting hours are over.]
    -> chen_deny

=== chen_allow ===
#speaker: Chen Huang
…Thanks.

For what it’s worth,
I appreciate professionalism.

~ SetEventVar("allowed_inside", true)
~ SpiritAngered = SpiritAngered + 1
~ Sanity = Sanity - 5
-> END

=== chen_deny ===
#speaker: Chen Huang
Figures.
Didn't even want to be here.
Have fun watching over these dead people.
I don't envy you.

~ SetEventVar("allowed_inside", false)
-> END
