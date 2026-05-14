### New Rules

 Rule ID    | Category            | Severity | Notes                                                                  
------------|---------------------|----------|------------------------------------------------------------------------
 CAERIUS001 | CaeriusNet.Analyzer | Error    | Type must be sealed.                                                   
 CAERIUS002 | CaeriusNet.Analyzer | Error    | Type must be partial.                                                  
 CAERIUS003 | CaeriusNet.Analyzer | Error    | Type must declare a primary constructor with parameters.               
 CAERIUS004 | CaeriusNet.Analyzer | Error    | [GenerateTvp] requires a non-empty TvpName.                            
 CAERIUS005 | CaeriusNet.Analyzer | Warning  | Parameter type falls back to sql_variant.                              
 CAERIUS006 | CaeriusNet.Analyzer | Error    | Generator target must be a non-generic top-level type.                 
 CAERIUS200 | CaeriusNet.Analyzer | Error    | Contract manifest must be present when contract generation is enabled. 
 CAERIUS201 | CaeriusNet.Analyzer | Error    | Procedure must be present in the contract manifest.                    
 CAERIUS202 | CaeriusNet.Analyzer | Error    | Referenced TVP must be present in the contract manifest.               
 CAERIUS203 | CaeriusNet.Analyzer | Error    | TVP column SQL type must be supported.                                 
 CAERIUS204 | CaeriusNet.Analyzer | Error    | First result set must be determinable.                                 
 CAERIUS205 | CaeriusNet.Analyzer | Warning  | Procedure has no result set.                                           
 CAERIUS206 | CaeriusNet.Analyzer | Error    | Output parameters are not supported by generated contracts.            
 CAERIUS207 | CaeriusNet.Analyzer | Error    | SQL type must be mappable to generated C# code.                        
 CAERIUS208 | CaeriusNet.Analyzer | Warning  | Nullable result column is emitted as nullable CLR type.                
 CAERIUS209 | CaeriusNet.Analyzer | Error    | Contract hash mismatch detected in verify mode.                        
 CAERIUS210 | CaeriusNet.Analyzer | Warning  | Procedure may be incompatible with generated contracts.                
