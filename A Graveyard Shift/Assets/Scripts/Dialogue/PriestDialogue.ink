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
A small thing, by most standards. A trinket.

It was a rosary, made of old wood and silver.
It was many years old.
It's been passed down through many sermons and confessions.
And on my final night here, I left it beneath the church.
In the basement.

+ [You came all this way for a rosary?]
    -> rosary_reason
+ [Why is it so important?]
    -> hiding_something
+ [You could buy another.]
    ~player_mean += 1
    -> buy_another
    
=== rosary_reason ===
Well...
This rosary is important.
I must find it.
+ [You're hiding something.]
    -> hiding_something

=== buy_another ===
No. I cannot.
This rosary does not belong to me.


+ [You're hiding something.]
    -> hiding_something
    
=== hiding_something ===
Alright, I will be candid with you.
-> why_now

=== why_now ===
I had hoped not to return to this place.

But recently... I received a letter.
It was from a fellow priest, whom I thought had passed long ago.
He was betrothed to a woman, and their wedding was due at the church behind you.
There was a terrible accident, and it was believed that neither of them had survived.
As it turns out, he is still alive. He has sent me to retrieve the rosary that belonged to him.
It was a wedding gift, and it is very important to him.


+ [How can you trust a letter like that?]
    -> trust
+ [How can I trust you?]
    ~player_scared += 1
    ->hesitation

=== trust ===
It was in his hand writing.
I know he wrote the letter.
I must return his lost item.
I would like you to come with me to retrieve it.

-> ask_for_entry

=== hesitation ===
You are wise to hesitate.

So am I.

Every step toward that door feels like walking back into a memory that has been waiting patiently for my return.

But I must return his lost item.
I would like you to come with me to retrieve it.

-> ask_for_entry


=== ask_for_entry ===
Shall we go together?

+ [Alright. Let's go.]
    -> allow_entry
+ [I'm sorry father, but you should leave.]
    -> refusal_end
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

=== refusal_end ===
Very well.

I will not force your hand.

May God forgive us both.
 ~ SetEventVar("allowed_inside", false)


-> END



