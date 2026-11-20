export default {
  async fetch(request, env, ctx) {
    if (request.method !== "POST") {
      return new Response("Method not allowed", { status: 405 });
    }

    const prompt = `あなたはプログラミングキャンプ「Life is Tech! Camp」のオープニングを盛り上げる司会者の台本を生成するAIです。
以下の【ベース構成】に従って、元気でテンションの高いオープニング台本を作成し、指定のJSON形式で出力してください。

【ベース構成】
以下の7文の構成をベースに作成してください。＜＞で囲まれた部分はあなたが具体的に想像して生成してください。
構成の骨組みは保ちつつ、語尾やユーモアには自然なブレを持たせてください。

1. みなさーん！おはようございまーす！
2. さあ、Life is Tech! Camp始めましょう！！
3. 今回のテーマは「＜前に進む気持ちになれる短いテーマ＞」です！
4. みんなで＜テーマを行動に移す短い呼びかけ＞！
5. ここで、大事な注意点があります！
6. ＜水分補給、迷子、楽しむ、の3要素を簡潔に面白く＞
7. それじゃあ、各班の自己紹介タイムです！

【ルール】
1. 生成した台本を「1文ごとに」1つのまとまりとする。
2. 【重要】テンポ良く進めるため、1文は必ず「30文字以内」に収めること。
3. 【重要】文字数が増えるような過剰な改変や、長すぎる修飾語の追加は絶対に行わないこと。
   - 3文目のテーマは、挑戦・飛躍・開拓・限界突破のような、前進や成長を感じる漢字2字または4字の熟語を優先する。例をそのまま使い回さず、毎回自然な候補を考えること。
   - 熟語より自然で印象に残る場合は、「枠からはみ出る」のような短い行動フレーズもよい。テーマは子どもにも伝わる言葉にし、長くても10文字以内にすること。
   - 4文目はテーマを実際の行動につなげ、みんなで一歩踏み出したくなる呼びかけにすること。日本語として自然な文にすること。
4. 1文の中を順番に分割すること。分割した "word" をすべて連結すると "sentence" と完全に一致するようにし、文字や空白を省略・重複させないこと。
   - リンクにする部分（"correct" と "miss"）は、単独で押す対象として成立するまとまりにすること。
   - 「今回の」「各班の」「あります！」のように、まとまりとして意味がある部分は分割せず、"correct" または "miss" のリンクにすること。
   - "plain" にするのは、分割した "word" が助詞、または「です」「ます」などの丁寧語尾と、それに続く句読点・感嘆符だけからなる場合に限る。句読点だけを独立させないこと。
   - 「始めましょう！」のように、観客がそのまま復唱して盛り上がる表現は "correct" にできる。
   - 固有名詞は、極力それ単独のまとまりにすること。
5. 分割した各要素に対して、"word", "type" ("correct"、"miss"、"plain" のいずれか), "gaya" を付与する。
   - "correct": 司会者がその "word" を言った直後に、観客が同じ言葉をそのまま復唱しても自然な掛け声になり、場が盛り上がるものだけ。例：「おはようございまーす！」「限界突破！」「一歩踏み出そう！」「迷子ゼロ！」。
   - "correct" の "gaya" は、短く元気な返答にし、句読点・記号・空白も含めて必ず12文字以内にすること。実際の "gaya" は言い換えてもよいが、"correct" 判定は必ず "word" そのものの復唱で行うこと。
   - "miss": 言い換えれば返答できても、その "word" をそのまま復唱しては盛り上がらない表現。重要な語を含むだけでは "correct" にしない。例：「みなさーん！」「テーマは、」「ここで、」「大事な注意点が」「あります！」「各班の」。"gaya" は空文字にする。
   - 各文に "correct" を必ず入れる必要はない。復唱で盛り上がる部分がなければ、その文は "miss" と "plain" のみでよい。
   - "plain": 単独の助詞、または単独の「です」「ます」などの丁寧語尾。ゲーム内ではリンクにせず、そのまま表示する。"gaya" は空文字にする。

【出力フォーマット】
以下のJSON配列のみを出力し、JSON以外の解説は一切含めないこと。

[
  {
    "sentence": "みなさーん！おはようございまーす！",
    "words": [
      { "word": "みなさーん！", "type": "miss", "gaya": "" },
      { "word": "おはようございまーす！", "type": "correct", "gaya": "おはよう！！" }
    ]
  },
  {
    "sentence": "さあ、Life is Tech! Camp始めましょう！！",
    "words": [
      { "word": "さあ、", "type": "miss", "gaya": "" },
      { "word": "Life is Tech! Camp", "type": "correct", "gaya": "いぇーい！！" },
      { "word": "始めましょう！！", "type": "correct", "gaya": "おー！！" }
    ]
  },
  {
    "sentence": "今回のテーマは、「限界突破」です！",
    "words": [
      { "word": "今回の", "type": "miss", "gaya": "" },
      { "word": "テーマは、", "type": "miss", "gaya": "" },
      { "word": "「限界突破」", "type": "correct", "gaya": "限界突破！" },
      { "word": "です！", "type": "plain", "gaya": "" }
    ]
  },
  {
    "sentence": "みんなで一歩踏み出そう！最高のキャンプにしよう！",
    "words": [
      { "word": "みんなで", "type": "miss", "gaya": "" },
      { "word": "一歩踏み出そう！", "type": "correct", "gaya": "踏み出そう！" },
      { "word": "最高のキャンプにしよう！", "type": "correct", "gaya": "最高にしよう！" }
    ]
  },
  {
    "sentence": "ここで、大事な注意点があります！",
    "words": [
      { "word": "ここで、", "type": "miss", "gaya": "" },
      { "word": "大事な注意点が", "type": "miss", "gaya": "" },
      { "word": "あります！", "type": "miss", "gaya": "" }
    ]
  },
  {
    "sentence": "水分補給！迷子ゼロ！全力で楽しもう！",
    "words": [
      { "word": "水分補給！", "type": "correct", "gaya": "水分補給！" },
      { "word": "迷子ゼロ！", "type": "correct", "gaya": "迷子ゼロ！" },
      { "word": "全力で楽しもう！", "type": "correct", "gaya": "楽しもう！" }
    ]
  },
  {
    "sentence": "それじゃあ、各班の自己紹介タイムです！",
    "words": [
      { "word": "それじゃあ、", "type": "miss", "gaya": "" },
      { "word": "各班の", "type": "miss", "gaya": "" },
      { "word": "自己紹介タイム", "type": "correct", "gaya": "自己紹介タイム！" },
      { "word": "です！", "type": "plain", "gaya": "" }
    ]
  }
]`;

    const geminiUrl = `https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:generateContent?key=${env.GEMINI_API_KEY}`;
    
    const requestBody = {
      contents: [{
        parts: [{ text: prompt }]
      }],
      generationConfig: {
        responseMimeType: "application/json"
      }
    };

    try {
      const geminiResponse = await fetch(geminiUrl, {
        method: "POST",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify(requestBody)
      });

      const data = await geminiResponse.json();
      
      return new Response(JSON.stringify(data), {
        status: geminiResponse.status,
        headers: { 
          "Content-Type": "application/json",
          "Access-Control-Allow-Origin": "*" 
        }
      });
    } catch (error) {
      return new Response(JSON.stringify({ error: error.message }), { status: 500 });
    }
  }
};
