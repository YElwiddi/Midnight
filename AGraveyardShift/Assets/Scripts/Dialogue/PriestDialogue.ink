EXTERNAL SetEventVar(varName, value)
EXTERNAL SetGameBoolFlag(flagName, value)
EXTERNAL SuspendDialogue()

VAR player_mean = 0
VAR player_scared = 0
VAR player_stupid = 0
VAR SpiritAngered = 0
VAR GraveRobberSetup = 0
VAR Sanity = 100
VAR GraveyardProtection = 100

=== start ===
Good evening.
I'm Father Joseph.
I used to be the pastor here at this church.
I need to come inside for a moment.

+ [Why?]
    -> why
+ [What is your business here?]
    -> why
    
=== why ===
Well...
I've left something important in the basement of that abandoned church years ago.
I need to come inside and reclaim it.

+ [What did you leave behind?]
    -> what_behind
+ [Why wait until now?]
    -> why_now

=== what_behind ===
A wedding ring for my betrothed. It remained here after many years.
I need it back.


+ [Are you hiding something?]
    -> hiding_something
+ [You came all this way just for that?]
    ~player_mean += 1
    -> buy_another
    


=== buy_another ===
Yes. It has very large sentimental value to me.
I need to enter the grounds to retrieve it.

+ [You're hiding something.]
    -> hiding_something
    
=== hiding_something ===
Alright, I will be candid with you.
-> why_now

=== why_now ===
I had hoped not to return to this place.

But recently... I have felt immense guilt over the loss of my wife to be.
She passed away before I had the opportunity to take our vows.
I must return to retrieve our wedding ring.
As something to remember her by.



+ [Are you lying?]
    -> trust
+ [How can I trust you?]
    ~player_scared += 1
    ->hesitation

=== trust ===
No. I know it has been many years since I've returned to these grounds...
But I'm back now and I need to retrieve what is rightfully mine.
So please...
-> ask_for_entry

=== hesitation ===
You are wise to hesitate.

Every step toward that church feels like walking back into a memory that has been waiting patiently for my return.

But I need that ring.
I would like you to come with me to retrieve it.

-> ask_for_entry


=== ask_for_entry ===
Shall we go together?

+ [Alright. Let's go.]
    -> allow_entry
+ [I'm sorry father, but you should leave.]
    -> refusal_end_1
+ [I have more questions.]
-> more_questions
  + [Hold on, I'll be right back.]
      ~ SuspendDialogue()
    -> ask_for_entry

=== more_questions ===
+ [What was your name again?]
    -> ask_name_again
+ [Do you have any family members?]
    -> how_many
+ [Nevermind.]
-> ask_for_entry

=== ask_name_again ===
My name is Father Joseph Smith.

-> more_questions

=== how_many ===
I have no immediate family members.
-> more_questions

=== allow_entry ===
Thank you.

We will not be long.
It is down the hall, on the staircase to the right.

I know the way.
...

I hope we are not too late.
Hurry.

 ~ SetEventVar("allowed_inside", true)
 -> END
 
=== refusal_end_1 ===
...
I urge you to reconsider.
It is very important you allow me inside to retrieve the lost item.
Please. Allow me in.
+[You're right. I'm sorry. Let's go inside.]
    ->allow_entry
+[No. Leave.]
    ->refusal_end_2

=== refusal_end_2 ===
Very well.

I will not force your hand.

May God forgive us both.
 ~ SetEventVar("allowed_inside", false)


-> END



