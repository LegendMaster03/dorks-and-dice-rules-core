// Reproduce the publisher-layout Animals import from already pinned native page records.
// The existing official-source extraction workflow independently checks those page records.
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
function generate(pages, candidates, manifest) {
  const norm=s=>s.normalize("NFKC").replace(/[’‘]/g,"'").replace(/[−–]/g,"-").replace(/\s+/g," ").trim();
  const slug=s=>norm(s).toLowerCase().replace(/[^a-z0-9]+/g,"-").replace(/^-|-$/g,"");
  const byName=new Map(candidates.map((r,i)=>[r.name.toLowerCase(),{r,i}]));
  const lines=[], boundaries=[];
  for(const p of pages.filter(p=>p.page>=344&&p.page<=364)){
    const raw=p.textLayout.split("\n").filter(l=>!l.includes("System Reference Document"));
    const counts={};
    for(const l of raw)for(const m of l.matchAll(/\S(?:.*?\S)?(?= {3,}|$)/g))if(m.index>=62&&m.index<=76)counts[m.index]=(counts[m.index]??0)+1;
    const ranked=Object.entries(counts).sort((a,b)=>b[1]-a[1]);
    if(!ranked.length||ranked[0][1]<15||ranked[0][1]<2*(ranked[1]?.[1]??0))throw Error("Ambiguous page gutter "+p.page);
    const split=Number(ranked[0][0]);boundaries.push({page:p.page,split});
    for(let side=0;side<2;side++)for(const l of raw){const t=(side?l.slice(split):l.slice(0,split)).trim();if(t)lines.push({page:p.page,side,t});}
  }
  const starts=[];
  for(let i=0;i<lines.length-2;i++){const candidate=byName.get(lines[i].t.toLowerCase());if(candidate&&/^(Tiny|Small|Medium|Large|Huge|Gargantuan)\b/.test(lines[i+1].t)&&/^AC\s/.test(lines[i+2].t))starts.push({layoutIndex:i,...candidate});}
  // Count publisher headings independently of the third-party names. A source
  // omission must be visible, not silently reduce the extracted inventory.
  const official=[];
  for(let i=0;i<lines.length-2;i++)if(/^(Tiny|Small|Medium|Large|Huge|Gargantuan) .+, .+$/.test(lines[i+1].t)&&/^AC \d+.*Initiative /.test(lines[i+2].t))official.push(lines[i].t);
  const first=official.indexOf("Allosaurus");
  if(first<0)throw Error("Animals section not found");
  const officialNames=official.slice(first);
  const expected=candidates.filter(x=>x.page>=344&&x.name!=="Ogre Zombie");
  const records=[],unresolved=[],differences=[];
  const conditionNames=new Set(["blinded","charmed","deafened","exhaustion","frightened","grappled","incapacitated","invisible","paralyzed","petrified","poisoned","prone","restrained","stunned","unconscious"]);
  const sizeCodes={Tiny:"T",Small:"S",Medium:"M",Large:"L",Huge:"H",Gargantuan:"G"};
  const alignmentCodes={Unaligned:["U"],"Neutral Good":["N","G"],Neutral:["N"],"Neutral Evil":["N","E"]};
  const groupKeys={"Traits":"trait","Actions":"action","Bonus Actions":"bonus","Reactions":"reaction"};
  const list=s=>s.split(/,\s*/).map(x=>x.trim().toLowerCase());
  const equal=(a,b)=>JSON.stringify(a)===JSON.stringify(b);
  for(let k=0;k<starts.length;k++){
    const start=starts[k].layoutIndex;
    const end=starts[k+1]?.layoutIndex??lines.length;
    const candidate=starts[k].r;
    if(candidate.name==="Ogre Zombie")continue;
    try{
      const block=lines.slice(start,end);const text=block.map(x=>x.t);const name=candidate.name;
      const require=(condition,message)=>{if(!condition)throw Error(message)};
      require(text[0]===name,"Heading mismatch");
      const m=norm(text[1]).match(/^(Tiny|Small|Medium|Large|Huge|Gargantuan) (.+), (.+)$/);
      require(m&&alignmentCodes[m[3]],"Type or alignment");
      let type;
      if(m[2]==="Swarm of Tiny Beasts")type={type:"beast",swarmSize:"T"};
      else if(m[2]==="Beast (Dinosaur)")type={type:"beast",tags:["dinosaur"]};
      else {require(/^(Beast|Monstrosity|Celestial)$/.test(m[2]),"Unsupported type");type=m[2].toLowerCase();}
      const ac=norm(text[2]).match(/^AC (\d+) Initiative ([+-]\d+) \((\d+)\)$/);
      const hp=norm(text[3]).match(/^HP (\d+) \(([^)]+)\)$/);
      require(ac&&hp,"AC/HP/initiative");
      const crIndex=text.findIndex(t=>/^CR /.test(t));
      require(crIndex>5,"Missing CR");
      const cr=norm(text[crIndex]).match(/^CR (\d+(?:\/\d+)?) \(XP ([\d,]+); PB ([+-]\d+)\)$/);
      require(cr,"Malformed CR");
      const speedText=norm(text[4]);require(speedText.startsWith("Speed "),"Missing speed");
      let speed={};
      for(const part of speedText.slice(6).split(", ")){
        const move=part.match(/^(?:(Climb|Burrow|Fly|Swim) )?(\d+) ft\.$/);
        if(move)speed[(move[1]??"walk").toLowerCase()]=Number(move[2]);
        else {const choice=part.match(/^Climb or Fly (\d+) ft\. \(GM's choice\)$/);require(choice,"Unsupported movement: "+part);speed.choose={from:["climb","fly"],amount:Number(choice[1]),note:"(GM's choice)"};}
      }
      const header=text.slice(5,crIndex);const abilityText=norm(header.join(" "));
      const scores={},save={},mods={};
      for(const a of ["Str","Dex","Con","Int","Wis","Cha"]){
        const match=abilityText.match(new RegExp("\\b"+a+" (\\d+) ([+-]\\d+) ([+-]\\d+)\\b"));
        require(match,"Missing ability "+a);const key=a.toLowerCase();scores[key]=Number(match[1]);mods[key]=Number(match[2]);save[key]=match[3];require(mods[key]===Math.floor((scores[key]-10)/2),"Invalid modifier");
      }
      const fields={};let field=null;
      for(const line of header){
        if(/^MOD SAVE|^(Str|Dex|Con|Int|Wis|Cha) \d/.test(line))continue;
        const match=line.match(/^(Skills|Senses|Languages|Resistances|Immunities|Vulnerabilities|Gear) (.*)$/);
        if(match){field=match[1];require(!(field in fields),"Duplicate "+field);fields[field]=match[2];}
        else {require(field,"Unexpected header line "+line);fields[field]+=" "+line;}
      }
      const skill={};if(fields.Skills)for(const s of norm(fields.Skills).split(", ")){const match=s.match(/^(.+) ([+-]\d+)$/);require(match,"Unparsed skill "+s);skill[match[1].toLowerCase()]=match[2];}
      const senses=norm(fields.Senses??"").match(/^(?:(.*?); )?Passive Perception (\d+)$/);require(senses,"Unparsed senses");
      require(fields.Languages,"Missing languages");
      const immunity=list(fields.Immunities??"").filter(Boolean), conditionImmune=immunity.filter(x=>conditionNames.has(x)), immune=immunity.filter(x=>!conditionNames.has(x));
      const pageNumbers=[...new Set(block.map(x=>x.page))];
      require(pageNumbers[0]===candidate.page,"Candidate page mismatch");
      const id="monster."+slug(name);
      const rec={id,kind:"monster",name,size:[sizeCodes[m[1]]],type,alignment:alignmentCodes[m[3]],ac:[Number(ac[1])],hp:{average:Number(hp[1]),formula:hp[2]},speed,...scores,save,initiative:{modifier:Number(ac[2]),score:Number(ac[3])},proficiencyBonus:Number(cr[3]),cr:cr[1],challenge:{rating:cr[1],xp:Number(cr[2].replace(/,/g,""))},...(Object.keys(skill).length?{skill}:{}),...(senses[1]?{senses:senses[1].split(", ")}:{}),passive:Number(senses[2]),...(fields.Languages==="None"?{}:{languages:[norm(fields.Languages)]}),...(fields.Resistances?{resist:list(fields.Resistances)}:{}),...(fields.Vulnerabilities?{vulnerable:list(fields.Vulnerabilities)}:{}),...(immune.length?{immune}:{}),...(conditionImmune.length?{conditionImmune}:{}),officialTextLayout:text.join("\n")};
      const actualGroups={};let group=null;
      for(const line of text.slice(crIndex+1)){
        if(groupKeys[line]){group=groupKeys[line];require(!actualGroups[group],"Repeated section "+line);actualGroups[group]=[];}
        else {require(group,"Unclassified body "+line);actualGroups[group].push(line);}
      }
      const title=s=>s.replace(/\{@recharge(?: (\d))?\}/g,(_,n)=>n&&n!=="6"?`(Recharge ${n}–6)`:"(Recharge 6)");
      for(const key of Object.values(groupKeys)){
        const expectedTitles=(candidate[key]??[]).map(x=>title(x.name));
        if(key==="action"&&candidate.spellcasting)expectedTitles.push(...candidate.spellcasting.map(x=>x.name));
        const body=actualGroups[key]??[];
        const positions=[];
        for(let j=0;j<body.length;j++){
          const label=expectedTitles.find(t=>norm(body[j]).startsWith(norm(t)+". "));
          if(label)positions.push({j,label});
        }
        require(positions.length===expectedTitles.length&&new Set(positions.map(x=>x.label)).size===expectedTitles.length&&(!body.length||positions[0]?.j===0),"Section title discrepancy: "+key+" expected "+expectedTitles.join(", ")+" found "+positions.map(x=>x.label).join(", "));
        if(!body.length)continue;
        rec[key]=positions.map(({j,label},n)=>({name:label,entries:[body.slice(j,positions[n+1]?.j??body.length).join("\n").slice(body[j].indexOf(". ")+2)]}));
      }
      const refs=new Set(pageNumbers.map(p=>"rulebook.page-"+String(p).padStart(3,"0")));
      for(const c of conditionImmune)refs.add("condition."+c);
      if(candidate.spellcasting){
        require(name==="Giant Owl","Unreviewed spellcasting structure");
        const spell=rec.action.find(x=>x.name==="Spellcasting");
        const body=norm(spell.entries[0]);
        require(body.includes("At Will: Detect Evil and Good, Detect Magic")&&body.includes("1/Day: Clairvoyance")&&body.includes("Wisdom"),"Spellcasting text changed");
        rec.spellcasting=[{name:"Spellcasting",type:"spellcasting",headerEntries:[spell.entries[0].split("\nAt Will:")[0]],will:["{@spell Detect Evil and Good|SRD52}","{@spell Detect Magic|SRD52}"],daily:{"1":["{@spell Clairvoyance|SRD52}"]},ability:"wis",displayAs:"action"}];
        rec.action=rec.action.filter(x=>x.name!=="Spellcasting");
        for(const s of ["detect-evil-and-good","detect-magic","clairvoyance"])refs.add("spell."+s);
      }
      // Compare candidate mechanics, but the publisher-derived values always control.
      for(const key of ["size","type","alignment","ac","hp","str","dex","con","int","wis","cha","passive","cr","skill","resist","immune","conditionImmune"]){
        const a=rec[key],b=key==="cr"&&typeof candidate.cr==="object"?candidate.cr.cr:candidate[key];if(a===undefined&&b===undefined)continue;
        if(!equal(a,b))differences.push({name,field:key,publisher:a??null,candidate:b??null});
      }
      if(typeof candidate.cr==="object"&&candidate.cr.xp!==undefined&&candidate.cr.xp!==rec.challenge.xp)differences.push({name,field:"challenge.xp",publisher:rec.challenge.xp,candidate:candidate.cr.xp});
      rec.catalogReferences=[...refs];rec.source={release:"srd-5.2.1",url:manifest.url,pdfSha256:manifest.officialPageImport.pdfSha256,pages:pageNumbers,section:"Animals",license:manifest.license,attribution:manifest.attribution,structuredSource:{provider:"CoolFireGiant/hewnhero-srd",commit:"d06d1dadee357857767b1e4da985df6609509bcf",path:"bestiary/bestiary-srd52.json",pointer:"/monster/"+byName.get(name.toLowerCase()).i},adaptation:"Direct publisher page-layout stat block; source candidate used for heading and section-entry identity comparison, not rule text."};
      records.push(rec);
    }catch(e){unresolved.push({name:candidate.name,reason:String(e.message??e)});}
  }
  const seen=new Set(records.map(x=>x.name));
  for(const r of expected)if(!seen.has(r.name)&&!unresolved.some(x=>x.name===r.name))unresolved.push({name:r.name,reason:"No publisher heading match"});
  const unmatchedOfficial=officialNames.filter(name=>!seen.has(name));
  if(officialNames.length!==new Set(officialNames).size)throw Error("Duplicate publisher headings");
  return {records,report:{officialHeadings:officialNames.length,unmatchedOfficial,edition:"srd-5.2.1",section:"Animals",expected:expected.length,extracted:records.length,unresolved,differences,boundaries}};
}
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),"..");
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),"utf8"));
const base="data/rulesets/dnd-5-5e/";
const manifest=read(base+"manifest.json");
const pages=[...read(base+"native/rulebook/pages-330-354.json"),...read(base+"native/rulebook/pages-355-364.json")];
manifest.url=pages[0].source.url;
const candidates=read("data/reference/srd/structured/bestiary/bestiary-srd52.json").monster;
const result=generate(pages,candidates,manifest);
if(result.report.unresolved.length||result.report.unmatchedOfficial.length||result.report.officialHeadings!==result.report.extracted)throw Error("Incomplete publisher Animals extraction: "+JSON.stringify(result.report));
const directory=path.join(root,base,"native/bestiary");
const json=value=>JSON.stringify(value,null,2)+"\n";
const files=new Map();
for(let i=0;i<result.records.length;i+=20){const number=String(i/20+1).padStart(2,"0");files.set(path.join(directory,"animals-"+number+".json"),json(result.records.slice(i,i+20)));}
files.set(path.join(root,"data/reference/srd/official-animals-import.json"),json(result.report));
if(process.argv.includes("--check")){for(const [file,body] of files)if(fs.readFileSync(file,"utf8")!==body)throw Error("Stale generated file: "+file);for(const file of fs.readdirSync(directory).filter(f=>/^animals-\\d+\\.json$/.test(f)))if(!files.has(path.join(directory,file)))throw Error("Stale animals file: "+file);}
else {for(const [file,body] of files){fs.mkdirSync(path.dirname(file),{recursive:true});fs.writeFileSync(file,body);}}
console.log(JSON.stringify(result.report));
