import assert from "node:assert/strict";
import test from "node:test";
import {linkCharacterRelationships} from "../tools/link_srd_character_relationships.mjs";

const source={release:"srd-5.2.1"};
const catalog=[
 {id:"baseitem.dagger",kind:"baseitem",name:"Dagger",source},
 {id:"feat.magic-initiate",kind:"feat",name:"Magic Initiate",source,_versions:[{name:"Magic Initiate; Cleric",source:"SRD52"}]},
 {id:"skill.arcana",kind:"skill",name:"Arcana",source},
 {id:"optional-feature.example",kind:"optional-feature",name:"Example",source}
];
test("links fixed equipment, proficiency choices and named feat variants without changing payload",()=>{
 const row={id:"background.example",catalogReferences:[],startingEquipment:{defaultData:[{A:[{item:"Dagger|SRD52",quantity:2}],B:[{equipmentType:"weaponSimple"}]}]},
  skillProficiencies:[{choose:{from:["arcana"],count:1}}],feats:[{"magic initiate; cleric|srd52":true}],
  entries:[{type:"refOptionalfeature",optionalfeature:"Example|SRD52"}]};
 const original=structuredClone(row);const result=linkCharacterRelationships([row],catalog,"SRD52");
 assert.deepEqual(result,{literalOccurrences:4,namedVariantOccurrences:1,addedCatalogLinks:4});
 assert.deepEqual(row.catalogReferences,["baseitem.dagger","feat.magic-initiate","optional-feature.example","skill.arcana"]);
 assert.deepEqual({...row,catalogReferences:[]},original);
});
test("rejects foreign and absent literal references",()=>{
 for(const raw of ["Dagger|SRD51","Missing|SRD52"]){
  assert.throws(()=>linkCharacterRelationships([{id:"example",startingEquipment:[raw]}],catalog,"SRD52"));
 }
});
